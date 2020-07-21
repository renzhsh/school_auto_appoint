using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AutoAppointApp
{
    public class AutoAppointService : IHostedService
    {
        private readonly CourseApi courseApi;
        private readonly ILogger<AutoAppointService> logger;
        private readonly GrabOrder grabService;
        private Timer timer;

        public AutoAppointService(CourseApi _courseApi, ILogger<AutoAppointService> _logger, GrabOrder grab)
        {
            courseApi = _courseApi;
            logger = _logger;
            grabService = grab;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(async (state) => await Schedule(), null, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(10));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            timer?.Dispose();
            return Task.CompletedTask;
        }

        public async Task Schedule()
        {
            await KeepAlive();
            grabService.RequestStartGrab();
        }

        public async Task KeepAlive()
        {
            logger.LogWarning("{0} KeepAlive", DateTime.Now);

            await courseApi.GetAvailableCourse(DateTime.Now.ToString("yyyy/MM/dd"));
        }
    }

    /// <summary>
    /// 抢单服务
    /// </summary>
    public class GrabOrder
    {
        private readonly CourseApi courseApi;
        private CancellationTokenSource GrabCancelSource;
        private readonly ILogger<GrabOrder> logger;
        private readonly IConfiguration _config;

        public const string OpenTime = "06:00:00";

        public GrabOrder(CourseApi _courseApi, ILogger<GrabOrder> _logger, IConfiguration configuration)
        {
            courseApi = _courseApi;
            logger = _logger;
            _config = configuration;
        }

        /// <summary>
        /// 运行中
        /// </summary>
        public bool Running { get => _nRun > 0; }
        private int _nRun = 0;

        /// <summary>
        /// 请求开启抢单服务
        /// </summary>
        public void RequestStartGrab()
        {
            var beginTime = DateTime.Parse($"{DateTime.Now.ToString("yyyy/MM/dd")} {OpenTime}").AddSeconds(-30);

            logger.LogWarning($"RequestStartGrab : {DateTime.Now} > {beginTime} ??");

            // 过了抢单时间段
            if (Running || DateTime.Now > beginTime) return;

            if (DateTime.Now.AddMinutes(15) >= beginTime)
            {
                logger.LogWarning("{0} 准备开始抢单...", DateTime.Now);
                Interlocked.Increment(ref _nRun);

                var source = new CancellationTokenSource(beginTime - DateTime.Now);

                source.Token.Register(async () => await Grab());
            }
        }

        public async Task Grab()
        {
            GrabCancelSource = new CancellationTokenSource();

            logger.LogWarning("{0} 抢单服务已启动", DateTime.Now);

            var today = DateTime.Now.AddDays(3).ToString("yyyy/MM/dd");

            var list = await courseApi.GetAvailableCourse(today);

            list = ReorderCourse(list);

            logger.LogWarning($"可用的课程数量为{list.Count()}");

#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            Task.Run(async () =>
            {
                logger.LogWarning("判断是否成功的线程已启动");
                while (!GrabCancelSource.Token.IsCancellationRequested)
                {
                    var success = await courseApi.IsAppointed(today);

                    logger.LogWarning($"{DateTime.Now} 抢单结果：{success}");

                    if (success)
                    {
                        logger.LogWarning("{0} 抢单成功", DateTime.Now);

                        CancelGrab();

                    }
                    else if (DateTime.Now > DateTime.Parse($"{DateTime.Now.ToString("yyyy/MM/dd")} {OpenTime}").AddMinutes(3)) // 退出抢票
                    {
                        logger.LogWarning("{0} 抢单失败", DateTime.Now);

                        CancelGrab();
                    }
                    else
                    {
                        Thread.Sleep(1000);
                    }
                }
            });
#pragma warning restore CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法

            var openTime = DateTime.Parse($"{DateTime.Now.ToString("yyyy/MM/dd")} {OpenTime}").AddSeconds(-1);

            if (DateTime.Now >= openTime)
            {
                callback();
            }
            else
            {
                var timer = new Timer((state) => callback(), null, openTime - DateTime.Now, TimeSpan.FromSeconds(0));
            }

            void callback()
            {
                foreach (var item in list)
                {
                    if (GrabCancelSource.Token.IsCancellationRequested) break;
                    if (item.PersonCount == 1) continue;

                    logger.LogWarning("{0} 启动新的时间段 [{1}]", DateTime.Now, item.BlockDescription);
                    GrabCourse(today, item);

                    Thread.Sleep(800); // 设置优先级
                }
            }
        }

        private void GrabCourse(string date, CourseBlock block)
        {
            for (var i = 0; i < 10; i++)
            {
                Task.Run(async () =>
                {
                    while (!GrabCancelSource.Token.IsCancellationRequested)
                    {
                        var ret = await courseApi.AppointCourse(date, block.BlockId);

                        if (!string.IsNullOrEmpty(ret.ErrorInfo))
                        {
                            if (ret.ErrorInfo.StartsWith("时间段预约人数已满"))
                            {
                                logger.LogError("{0} 当前时间段不可用 [{1}]", DateTime.Now, block.BlockDescription);
                                break;
                            }

                            if (ret.ErrorInfo.StartsWith("学员每日预约的时间段总数不能超过规定的上限"))
                            {
                                logger.LogError("{0} 当前时间段已预约成功 [{1}]", DateTime.Now, block.BlockDescription);
                                break;
                            }
                        }

                    }

                    logger.LogWarning("{0} 退出时间段 [{1}]", DateTime.Now, block.BlockDescription);
                });
            }
        }

        /// <summary>
        /// 退出抢单
        /// </summary>
        private void CancelGrab()
        {
            if (!GrabCancelSource.Token.IsCancellationRequested)
            {
                lock (this)
                {
                    if (!GrabCancelSource.Token.IsCancellationRequested)
                    {
                        GrabCancelSource.Cancel(false);
                        Interlocked.Decrement(ref _nRun);
                    }
                }
            }
        }


        /// <summary>
        /// 按照优先级重新排序
        /// </summary>
        /// <param name="courses"></param>
        /// <returns></returns>
        private IEnumerable<CourseBlock> ReorderCourse(IEnumerable<CourseBlock> courses)
        {
            var periods = _config.GetSection("TimePeriods").Get<string[]>();
            //var periods = new string[]
            //{
            //    "8:00-8:30",
            //    "8:30-9:00",
            //    "9:00-9:30",
            //    "9:30-10:00",
            //    "10:00-10:30",
            //    "10:30-11:00",
            //    "14:00-14:30",
            //    "14:30-15:00",
            //    "15:00-15:30",
            //    "15:30-16:00"
            //};

            return courses.Where(item => periods.Contains(item.BlockDescription));
        }
    }
}

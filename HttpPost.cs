using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace AutoAppointApp
{
    public class CourseApi
    {
        private readonly BaseInfo baseInfo;
        private readonly HttpClient httpClient;
        private readonly ILogger<CourseApi> logger;
        public CourseApi(IHttpClientFactory factory, ILogger<CourseApi> _logger, IOptions<BaseInfo> options)
        {
            httpClient = factory.CreateClient();
            baseInfo = options.Value;
            logger = _logger;
        }

        public async Task<T> Post<T>(string url, PostParamter args)
        {
            try
            {
                var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(args));

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    var result = JsonConvert.DeserializeObject<RetResult<T>>(content);

                    if (result.StatusCode == "No")
                    {
                        logger.LogError("ASP.NET_SessionId 已过期");
                        await UpateCookie();

                    }
                    else if (result.StatusCode == "Error" && !string.IsNullOrEmpty(result.ErrorInfo))
                    {
                        logger.LogError(result.ErrorInfo);
                    }

                    return result.ReturnValue;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, ex.Message);
            }

            return default;

        }

        /// <summary>
        /// 获取可预约的课程
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<IEnumerable<CourseBlock>> GetAvailableCourse(string date)
        {
            var url = "http://yy.masjp.cn/Home/ListCoachTimeBlocks";

            var parameter = new PostParamter();
            parameter.Add("schoolid", baseInfo.SchoolId)
                .Add("courseid", baseInfo.CourseId)
                .Add("studydate", date);

            var result = await Post<IEnumerable<CourseBlock>>(url, parameter);

            if (result == null)
            {
                result = new List<CourseBlock>();
            }

            return result.Where(item => item.PersonCount == 0);
        }

        /// <summary>
        /// 预约课程
        /// </summary>
        /// <param name="date"></param>
        /// <param name="blockId"></param>
        /// <returns></returns>
        public async Task AppointCourse(string date, int blockId)
        {
            var url = "http://yy.masjp.cn/Home/SubmitCourseOrder";

            var parameter = new PostParamter();
            parameter.Add("schoolid", baseInfo.SchoolId)
                .Add("studyorderid", baseInfo.OrderId)
                .Add("coachid", baseInfo.CoachId)
                .Add("courseid", baseInfo.CourseId)
                .Add("studydate", date)
                .Add("startblockid", blockId.ToString())
                .Add("blockcount", "1");

            await Post<string>(url, parameter);
        }

        /// <summary>
        /// 是否预约成功
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<bool> IsAppointed(string date)
        {
            var url = "http://yy.masjp.cn/Home/ListStudentAllCourseOrders";

            var parameter = new PostParamter();
            parameter.Add("schoolid", baseInfo.SchoolId)
                .Add("studystatusid", "0")
                .Add("studyorderid", "");

            var result = await Post<IEnumerable<MyCourse>>(url, parameter);

            if (result == null)
            {
                result = new List<MyCourse>();
            }

            return result.Where(item => DateTime.Parse(item.StudyDate) == DateTime.Parse(date)).Any();
        }

        public async Task UpateCookie()
        {
            var url = "http://yy.masjp.cn/Home/StudentLogin";

            var pwd = baseInfo.Password;
            var code = getCheckCode();

            pwd = encrypt(pwd);
            pwd = encrypt(code + pwd);

            var parameter = new PostParamter();
            parameter.Add("schoolid", baseInfo.SchoolId)
                .Add("username", baseInfo.UserName)
                .Add("pwd", pwd)
                .Add("checkcode", code)
                .Add("backurl", "/");

            var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(parameter));

            if (response.IsSuccessStatusCode)
            {
                var cookie = response.Headers.GetValues("Set-Cookie").FirstOrDefault();

                httpClient.DefaultRequestHeaders.Remove("Cookie");
                httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
            }
        }

        private string getCheckCode()
        {
            return Convert.ToInt64((DateTime.Now - DateTime.Parse("1970/1/1 00:00:00")).TotalMilliseconds).ToString();
        }

        private string encrypt(string input)
        {
            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                var strResult = BitConverter.ToString(result);
                return strResult.Replace("-", "").ToLower();
            }
        }

    }
}

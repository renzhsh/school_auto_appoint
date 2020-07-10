using System;
using System.Collections.Generic;
using System.Text;

namespace AutoAppointApp
{
    public class RetResult<T>
    {
        public string StatusCode { get; set; }

        public string ErrorInfo { get; set; }

        public int RecordCount { get; set; }

        public T ReturnValue { get; set; }
    }

    /// <summary>
    /// 预约课程
    /// </summary>
    public class CourseBlock
    {
        public int BlockId { get; set; }

        public string BlockDescription { get; set; }

        public DateTime StartTime { get; set; }

        public int PersonCount { get; set; }

        public override string ToString()
        {
            return $"[{BlockId}] {BlockDescription}";
        }
    }

    /// <summary>
    /// 已预约的课程
    /// </summary>
    public class MyCourse
    {
        /// <summary>
        /// 2020/7/10
        /// </summary>
        public string StudyDate { get; set; }

        /// <summary>
        /// 2020/7/10 18:00:00
        /// </summary>
        public string StartTime { get; set; }

        /// <summary>
        /// 18:00-18:30
        /// </summary>
        public string BlockDescription { get; set; }
    }

}

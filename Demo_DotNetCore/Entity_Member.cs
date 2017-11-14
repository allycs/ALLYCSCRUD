namespace Demo_DotNetCore
{
    using System;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// 因此表非该工程创建为取数据特此设定
    /// </summary>
    public class Member
    {
        [Key]
        public string Uid { get; set; }

        public string Openid { get; set; }
        public string OpenidWechat { get; set; }
        public string Nickname { get; set; }
        public string Photo { get; set; }
        public string Introduction { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public string RealName { get; set; }
        public DateTime LoginTime { get; set; }
        public string Token { get; set; }
        public string TokenWechat { get; set; }
        public int Verified { get; set; }
        public string BusinessCard { get; set; }
        public int Expert { get; set; }
        public string ExpertInfo { get; set; }
    }
}
using System;
using System.IO;
using System.Linq;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.Text.RegularExpressions;
using System.Globalization;

class Program
{
    static string dir = @"pic\";
    static bool sucess=false;
    static void Main()
    {
        Console.Title = "悟已往之不谏 知来者之可追";
        Process(dir);
        Console.WriteLine("Done (～￣▽￣)～ ");
        Console.ReadLine();
    }
    static void Process(string folder)
    {
        if (!System.IO.Directory.Exists(folder))
        {
            Console.WriteLine("请先在程序所在目录下创建pic文件夹 并将所有待转换媒体文件夹放入其中 再运行程序");
            Console.Read();
            Environment.Exit(0);
        }
        Console.WriteLine("部分较新的设备存储文件时不会将拍摄日期写入元数据 会尝试根据其他特征匹配");
        Console.WriteLine("故存在极小可能性出现日期修改错误 请务必提前备份重要照片 作者不为任何损失负责");
        Console.WriteLine("按enter同意并开始");
        Console.ReadLine();
        string[] media = System.IO.Directory.GetFiles(folder);
        string ext;
        foreach (string m in media)
        {
            try
            {
                ext=Path.GetExtension(m).ToLower();
                if (ext == ".jpg" || ext == ".jpeg"||ext==".heic")
                {
                    var directories = ImageMetadataReader.ReadMetadata(m);
                    var subIfdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                    if (subIfdDirectory != null && subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTime, out DateTime originalDateTime))
                    {
                        Set(m, originalDateTime);
                        sucess = true;
                    }
                }
                if (ext == ".mov" || ext == ".mp4")
                {
                    var directories = ImageMetadataReader.ReadMetadata(m);
                    var quickTimeDirectory = directories.OfType<MetadataExtractor.Formats.QuickTime.QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                    if (quickTimeDirectory != null && quickTimeDirectory.TryGetDateTime(MetadataExtractor.Formats.QuickTime.QuickTimeMovieHeaderDirectory.TagCreated, out DateTime creationDate))
                    {
                        Set(m,creationDate);
                    }
                }
                if (ext == ".png")//PNG无日期元数据项 可能在描述里
                {
                    var directories = ImageMetadataReader.ReadMetadata(m);
                    Regex regex_meta = new Regex(@"\d{4}:\d{2}:\d{2} \d{2}:\d{2}:\d{2}");
                    Match match_meta;
                    foreach (var d in directories)
                    {
                        foreach (var tag in d.Tags)
                        {
                            match_meta = regex_meta.Match(tag.Description.ToString());
                            if (match_meta.Success)
                            {
                                Set(m, DateTime.ParseExact(match_meta.Value, "yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture));
                                break;
                            }
                        }
                    }
                }
                if (!sucess && ext == ".jpg" || ext == ".jpeg" || ext == ".heic" || ext == ".mov" || ext == ".mp4" || ext == ".png")//都失败的方案 开始利用文件名推测
                {
                    if (DateTime.TryParseExact(TryStd(m), "yyyyMMddHHmmss", CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime t))
                    {
                        Set(m, t);
                    }
                }
                if (!sucess)
                {
                    Console.WriteLine("×未获取到日期信息：" + m);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"在修改 {m} 时遇到错误: {ex.Message}");
            }
            sucess = false;
        }
    }
    static string TryStd(string f)//yyyyMMddHHmmss标准格式   文件名推测
    {
        f=Path.GetFileNameWithoutExtension(f);
        string pattern = @"\d+"; 
        string result = "";
        MatchCollection matches = Regex.Matches(f, pattern);
        foreach (Match match in matches)
        {
            result += match.Value;
        }
        return Find(result);
    }
    static void Set(string m, DateTime t)//设置日期
    {
        File.SetCreationTime(m, t);
        File.SetLastWriteTime(m, t);
        Console.WriteLine("○修正成功：" + m);
        sucess = true;
    }
    static string Find(string input)//处理传入的纯数字 尝试转化为日期
    {
        for (int i = 0; i <= input.Length - 14; i++)
        {
            int year;
            if (!int.TryParse(input.Substring(i, 4), out year) || year < 1990 || year > Convert.ToInt32(DateTime.Now.ToString("yyyyMMddHHmmss").Substring(0,4)))//最小年份设置在1990年
                continue;
            string dateTimeString = input.Substring(i + 4, 10);
            if (Validate(dateTimeString))
            {
                return string.Format("{0:D4}{1}", year, dateTimeString);
            }
        }
        return "";
    }

    static bool Validate(string input)//验证时间合理性
    {
        int month;
        if (!int.TryParse(input.Substring(0, 2), out month) || month < 1 || month > 12)
            return false;
        int day;
        if (!int.TryParse(input.Substring(2, 2), out day) || day < 1 || day > DateTime.DaysInMonth(1990, month))//最小年份设置在1990年
            return false;
        int hour;
        if (!int.TryParse(input.Substring(4, 2), out hour) || hour < 0 || hour > 23)
            return false;
        int minute;
        if (!int.TryParse(input.Substring(6, 2), out minute) || minute < 0 || minute > 59)
            return false;
        int second;
        if (!int.TryParse(input.Substring(8, 2), out second) || second < 0 || second > 59)
            return false;
        return true;
    }
}

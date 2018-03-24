using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace PixivMasterReplacer
{
    class PixivMaster
    {
        public string filePath { set; get; }
        public string fileName { set; get; }
        public string workID { set; get; }
        public string pageNum { set; get; }
        public string fileType { set; get; }
        public string workUrl { set; get; }

        public string failMessage { set; get; }
        public string savePath { set; get; }

        public PixivMaster(string fullFilePath)
        {
            this.filePath = Path.GetDirectoryName(fullFilePath);
            this.fileName = Path.GetFileName(fullFilePath);
            this.workID = fileName.Split('_')[0];
            this.pageNum = new Regex(@"_p(.+?)_").Match(fileName).Groups[1].ToString();
            this.fileType = Path.GetExtension(fullFilePath);
            this.workUrl = String.Format("https://www.pixiv.net/member_illust.php?mode=medium&illust_id={0}", workID);
            this.savePath = string.Format("{0}\\{1}_p{2}", filePath, workID, pageNum);
        }

        public override string ToString()
        {
            return string.Format("filePath: {0}{6}fileName: {1}{6}workID: {2}{6}pageNum: {3}{6}workUrl: {4}{6}savePath: {5}", 
                filePath, fileName, workID, pageNum, workUrl, savePath, Environment.NewLine);
        }
    }
}

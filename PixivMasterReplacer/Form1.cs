using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace PixivMasterReplacer
{
    public partial class Form1 : Form
    {
        Regex pageNumRegex = new Regex(@"_p(.+?)_");
        Regex workIDRegex = new Regex(@"\\(.+?)_p");
        string[] fileTypes = new string[]{".jpg", ".png", ".gif"};
        List<PixivMaster> pixivMasterList = new List<PixivMaster>();
        List<string> failureList = new List<string>();
        string[] folderPaths;
        string user = "";
        string pass = "";
            
        public Form1()
        {
            InitializeComponent();
        }

        //Choose folder button
        private void button1_Click(object sender, EventArgs e)
        {
            reset();

            //Create the choose folder dialog
            var dialog = new CommonOpenFileDialog();
            dialog.AllowNonFileSystemItems = true;
            dialog.Multiselect = true;
            dialog.IsFolderPicker = true;
            dialog.Title = "Select folders with \"master\" images";

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            {
                MessageBox.Show("No Folder selected");
                return;
            }

            //This actually just grabs the path of the selected folder
            folderPaths = dialog.FileNames.ToArray();

            textBox1.Text = string.Join(", ", folderPaths);
        }

        private void replaceMastersBtn_Click(object sender, EventArgs e)
        {

            // USER INPUT HANDLING
            //if (string.IsNullOrWhiteSpace(username.Text)) { MessageBox.Show("Enter username"); return; }
            //if (string.IsNullOrWhiteSpace(password.Text)) { MessageBox.Show("Enter password"); return; }
            if (string.IsNullOrWhiteSpace(textBox1.Text)) { MessageBox.Show("Choose a folder"); return; }

           // user = username.Text;
           // pass = password.Text;

            createListOfMasters();
            downloadNonMasters();
            printResults();
            reset();
        }

        //Creates a List of PixivMaster objects from the selected folders
        private void createListOfMasters()
        {
            //Loop through selected folders
            foreach (var folderPath in folderPaths)
            {
                string[] currentFolderFiles = Directory.EnumerateFiles(folderPath, "*master*", SearchOption.AllDirectories).ToArray();

                //Loop through files in current folder
                for (int i = 0; i < currentFolderFiles.Length; i++)
                {
                    pixivMasterList.Add(new PixivMaster(currentFolderFiles[i]));
                }
            }
        }

        //Goes through all the images in the pixivMasterList and downloads the real version 
        private void downloadNonMasters()
        {
            log(pixivMasterList.Count + " file(s) found");
            log("");

            HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();
            string wrongUrl = ""; //A url that contains relevant info for the real url of the image
            string correctUrl = ""; //The url of the real image

            using (var client = new WebClient())
            {

                client.Headers.Set("Referer", "http://spapi.pixiv.net/");
                client.Headers.Set("User-Agent", "PixivIOSApp/5.1.1");
                client.Headers.Set("Content-Type", "application/x-www-form-urlencoded");
                client.Headers.Set("Authorization", "Bearer 8mMXXWT9iuwdJvsVIvQsFYDwuZpRCMePeyagSh30ZdU");
                client.Headers.Set("client_id", "bYGKuGVw91e0NMfPGp44euvGt59s");
                client.Headers.Set("client_secret", "HP3RmkgAmEGro0gn1x9ioawQE8WMfvLXDz3ZqxpK");
               // client.Headers.Set("username", user);   
               // client.Headers.Set("password", pass);    
                client.Headers.Set("grant_type", "password");  

                foreach (PixivMaster pMaster in pixivMasterList)
                {
                    log("File " + (pixivMasterList.IndexOf(pMaster) + 1) + " of " + pixivMasterList.Count + ": " +  pMaster.fileName);

                    //Used to extract the date part of the url to be used in grabbing the url of the real image
                    Regex getRelevantUrlInfo = new Regex(@"/img/(.+?)" + pMaster.workID);

                    //Maseter images are always .jpg but the real image might not be so you have to try all image types until it works
                    foreach (string fileType in fileTypes)
                    {
                        try
                        {
                            //Load the page that has the image into a HtmlAgility doc
                            document.LoadHtml(client.DownloadString(pMaster.workUrl));

                            wrongUrl = document.DocumentNode.Descendants("img")
                                .Where(s => s.GetAttributeValue("src", "").Contains(pMaster.workID))
                                .First().GetAttributeValue("src", "FAILED");

                            correctUrl = string.Format("https://i.pximg.net/img-original/img/{0}{1}_p{2}{3}",
                                getRelevantUrlInfo.Match(wrongUrl).Groups[1].ToString(),
                                pMaster.workID,
                                pMaster.pageNum,
                                fileType);

                            client.Headers.Set("Referer", "http://www.pixiv.net/");
                            client.DownloadFile(correctUrl, pMaster.savePath + fileType);

                            //Success! Delete master then continue to next image
                            File.Delete(pMaster.filePath + "\\" + pMaster.fileName); //filepath + filename
                            break;
                        }
                        catch (WebException ex)
                        {
                            log("Wrong file type (" + fileType + ") tried");

                            //Download failed even when using the last filetype, this means the work doesnt exist anymore
                            if (fileType.Equals(fileTypes[fileTypes.Length - 1]) && ex.Message.Contains("404"))
                            {
                                log("All file types tried");
                                string failure = string.Format("{0} failed to download.{2}That work might not exist anymore, check here: {2}{1}{2}", pMaster.fileName, pMaster.workUrl, Environment.NewLine);
                                failureList.Add(failure);
                            }
                        }
                    }
                }
            }
        }

        private void printResults()
        {
            int succ = pixivMasterList.Count - failureList.Count;

            log("------------------------------------------------------------------------------------------------");
            log("Process Complete!");
            log(succ + " out of " + pixivMasterList.Count + " image(s) downloaded successfully");

            if (failureList.Count > 0)
            {
                log("------------------------------------------------------------------------------------------------");
                log("Failures: ");
                for (int i = 0; i < failureList.Count; i++)
                {
                    log(failureList[i]);
                }
            }

            log("");
        }

        private void log(string logText)
        {
            output.AppendText(logText + "\n");
        }

        private void reset()
        {
            pixivMasterList.Clear();
            failureList.Clear();
        }

    }
}

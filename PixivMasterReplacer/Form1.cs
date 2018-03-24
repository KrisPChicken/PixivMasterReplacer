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

        public Form1()
        {
            InitializeComponent();
        }

        //Choose folder button
        private void button1_Click(object sender, EventArgs e)
        {
            //Reset
            output.Clear();
            pixivMasterList.Clear();
            failureList.Clear();

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
            if (string.IsNullOrWhiteSpace(username.Text)) { MessageBox.Show("Enter username"); return; }
            if (string.IsNullOrWhiteSpace(password.Text)) { MessageBox.Show("Enter password"); return; }
            if (string.IsNullOrWhiteSpace(textBox1.Text)) { MessageBox.Show("Choose a folder"); return; }

            createListOfMasters();
            downloadNonMasters();
            printResults();
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
                client.Headers.Set("username", "jockman452");   //HARDCODED CHANGE LATER
                client.Headers.Set("password", "925438143");    //HARDCODED CHANGE LATER
                client.Headers.Set("grant_type", "password");   //HARDCODED CHANGE LATER


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
                            //File.Delete(pMaster.filePath + "\\" + pMaster.fileName); //filepath + filename
                            break;
                        }
                        catch (WebException ex)
                        {
                            //Download failed even when using the last filetype, this means the work doesnt exist anymore
                            if (fileType.Equals(fileTypes[fileTypes.Length - 1]) && ex.Message.Contains("404"))
                            {
                                string failure = string.Format("{0} failed to download. That work might not exist anymore, check here: {2}{1}{2}", pMaster.fileName, pMaster.workUrl, Environment.NewLine);
                                failureList.Add(failure);
                            }
                            else
                            {
                                log("Wrong file type (" + fileType + ") tried");
                            }
                        }
                    }
                }
            }
        }

        private void printResults()
        {
            int succ = pixivMasterList.Count - failureList.Count;

            log("------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
            log("Process Complete!");
            log(succ + " out of " + pixivMasterList.Count + " image(s) downloaded successfully");

            if (failureList.Count > 0)
            {
                log("------------------------------------------------------------------------------------------------------------------------------------------------------------------------------");
                log("Failures: ");
                for (int i = 0; i < failureList.Count; i++)
                {
                    log(failureList[i]);
                }
            }
        }

        /*
        private void replaceMasterFiles(string[] filePaths)
        {
            
            //List<string> failures = new List<string> {};    //List of images that failed replacing
            //string currentFolder = "";                      //Full path to the current image
           // string masterName = "";                         //Full file name of current image
            //string workID = "";                             //Work ID of the current image
           // string pageNum = "";                            //Page number of the current image (for works with more than one image)
           // string fileType = "";                           //File type of current image
             
            ///////////////////////////////////////////////////////////
            //FIX THE ERRORS THAT CAME FROM COMMENTINT OUT SHIT ABOVE//
            ///////////////////////////////////////////////////////////

            log("Grabbing files from " + filePaths);//output.AppendText("Grabbing files from " + folder + "\n");
            log(filePaths.Length + " file(s) found");//output.AppendText(files.Length + " file(s) found\n");

            foreach(var filePath in filePaths)
            {
                PixivMaster currentImage = new PixivMaster(filePath);



                //currentFolder = Path.GetDirectoryName(filePath);
                //masterName = Path.GetFileName(filePath);
                //workID = masterName.Split('_')[0];
                //pageNum = pageNumRegex.Match(filePath).Groups[1].ToString();
                //fileType = Path.GetExtension(filePath);

                log("Current Folder: " + currentImage.filePath);
                log("File Name: " + currentImage.fileName);
                log("Work ID: " + currentImage.workID);
                log("Page Number: " + currentImage.pageNum);

                //Save the non master version of the image. If it gets saved succesfully delete the master version
                if (saveNonMaster(workID, pageNum, currentFolder, masterName, fileType))
                {
                    File.Delete(currentFolder + "\\" + masterName); //filepath + filename
                }
                else
                {
                    failures.Add(masterName);
                }
            }

            log("OPERATION COMPLETE");

            if(failures.Count > 0){
                log(failures.Count + " file(s) failed to replace:");
                
                foreach (var failure in failures)
                {
                    log(failure);
                }
            }
        }
        */

        /*
        private bool saveNonMaster(string workID, string pageNum, string currentFolder, string masterName, string fileType)
        {
            Regex getRelevantUrlInfo = new Regex(@"/img/(.+?)" + workID);
            string workUrl = String.Format("https://www.pixiv.net/member_illust.php?mode=medium&illust_id={0}", workID);
            string wrongUrl = "";
            string wrongUrlParsed = "";
            string correctUrl = "";
            string savePath = string.Format("{0}\\{1}_p{2}", currentFolder, workID, pageNum);

            log("Save Path: " + savePath);
            log("File Type: " + fileType);
            log("Pixiv Image URL: " + workUrl);
            
            using (var client = new WebClient())
            {
                try
                {
                    //Sets the headers of the web client
                    client.Headers.Set("Referer", "http://spapi.pixiv.net/");
                    client.Headers.Set("User-Agent", "PixivIOSApp/5.1.1");
                    client.Headers.Set("Content-Type", "application/x-www-form-urlencoded");
                    client.Headers.Set("Authorization", "Bearer 8mMXXWT9iuwdJvsVIvQsFYDwuZpRCMePeyagSh30ZdU");
                    client.Headers.Set("Cookie", "PHPSESSID=6949838_8f037c07053ed001b89813fcf81ff7fc");//OLD: 6949838_f88bbed126a39064b83a3d1608edb9a2
                    client.Headers.Set("client_id", "bYGKuGVw91e0NMfPGp44euvGt59s");
                    client.Headers.Set("client_secret", "HP3RmkgAmEGro0gn1x9ioawQE8WMfvLXDz3ZqxpK");
                    client.Headers.Set("username", "jockman452");
                    client.Headers.Set("password", "925438143");
                    client.Headers.Set("grant_type", "password");
                   
                    string source = client.DownloadString(workUrl);
                    //End try here?



                    HtmlAgilityPack.HtmlDocument document = new HtmlAgilityPack.HtmlDocument();
                    document.LoadHtml(source);

                    wrongUrl = document.DocumentNode.Descendants("img")
                        .Where(s => s.GetAttributeValue("src", "").Contains(workID))
                        .First().GetAttributeValue("src", "FAILED");

                    wrongUrlParsed = getRelevantUrlInfo.Match(wrongUrl).Groups[1].ToString();


                    log("Wrong URL: " + wrongUrl);
                    log("Wrong URL Parsed: " + wrongUrlParsed);


                    //New problem: all master images are saved as .jpg even if their actual version is not

                    for (int i = 0; i < fileTypes.Length; i++)
                    {
                        fileType = fileTypes[i];
                        try
                        {
                            correctUrl = string.Format("https://i.pximg.net/img-original/img/{0}{1}_p{2}{3}", wrongUrlParsed, workID, pageNum, fileType);
                            log("Computed Correct URL: " + correctUrl);
                            log("=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=");

                            client.Headers.Set("Referer", "http://www.pixiv.net/");
                            client.DownloadFile(correctUrl, savePath + fileType);

                            return true;
                        }
                        catch (Exception fileTypeException)
                        {

                            log("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                            log("ERROR MESSAGE(S): " + fileTypeException.InnerException);
                            log("==========");
                            log(fileTypeException.ToString());
                            log("==========");
                            log(fileTypeException.Message);
                            log("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

                            log("Wrong file type... trying next file type");
                            if(fileType.Equals(fileTypes[2])){
                                log("All file types tried no success");
                                return false;
                            }
                        }
                    }

                }
                catch (WebException ex) 
                {
                    if(ex.Message.Contains("404"))
                    {
                        log("404 Error! The work was most likely deleted");
                    }
                    return false; 
                }
                
            }//End WebClient
            return true;
        }
        */

        public void log(string logText)
        {
            output.AppendText(logText + "\n");
        }

    }
}

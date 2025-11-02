using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
namespace Common.Resources
{
    public class FolderKeywords
    {
        public const string META_FOLDER = "Meta";
        public const string QUESTION_FOLDER = "Q";
        public static readonly string baseDirectory = GetProjectRootDirectory();
        //question
        public static readonly string HTTP_Q = Path.Combine(baseDirectory, "Resources", "ProjectResource", "HTTP", "Question");
        public static readonly string TCP_Q = Path.Combine(baseDirectory, "Resources", "ProjectResource", "TCP", "Question");

        //TestCase
        public static readonly string HTTP_TC = Path.Combine(baseDirectory, "Resources", "ProjectResource", "HTTP", "TestCase");
        public static readonly string TCP_TC = Path.Combine(baseDirectory, "Resources", "ProjectResource", "TCP", "TestCase");
        private static string GetProjectRootDirectory()
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            string projectRoot = Directory.GetParent(currentDirectory)?.Parent?.Parent?.FullName;

            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new DirectoryNotFoundException("Project root directory not found.");
            }

            return projectRoot;
        }
    }
}

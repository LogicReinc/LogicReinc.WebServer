
using LogicReinc.WebServer.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicReinc.Web.Tests.Compoenents
{
    [TestClass]
    public class MultiPartTests
    {
        const string EXAMPLE = @"-----------------------------9051914041544843365972754266
Content-Disposition: form-data; name=""text""

text default
-----------------------------9051914041544843365972754266
Content-Disposition: form-data; name=""file1""; filename=""a.txt""
Content-Type: text/plain

Content of a.txt.

-----------------------------9051914041544843365972754266
Content-Disposition: form-data; name=""file2""; filename=""a.html""
Content-Type: text/html

<!DOCTYPE html><title>Content of a.html.</title>

-----------------------------9051914041544843365972754266--
";
        static byte[] exampleBytes = Encoding.UTF8.GetBytes(EXAMPLE);

        [ClassInitialize]
        public static void Initialize(TestContext context)
        {

        }

        [TestMethod]
        public void TestMultipart()
        {
            MultiPart parts;
            using (MemoryStream stream = new MemoryStream(exampleBytes))
            {
                parts = MultiPart.Parse(stream);
            }

            foreach(MultiPartSection section in parts.Sections)
            {
                Console.WriteLine("New Section");
                Console.WriteLine($"FileName: {section.FileName}");
                Console.WriteLine($"Name: {section.Name}");
                Console.WriteLine($"Content-Type: {section.ContentType}");
                Console.WriteLine($"Content: {section.DataAsString}");
                Console.WriteLine("-------------------");
            }
        }

        [TestMethod]
        public void CreateMultipartTestFile()
        {
            byte[] buffer = new byte[4096];
            int read = 0;
            string boundary = "!-----Test";
            string testFile = "Testfile3";

            File.WriteAllText("TestMultipart", boundary + @"
Content-Disposition: form-data; name=""file1""; filename=""testFile.test""
Content-Type: application/octet-stream

");

            using (FileStream str = new FileStream(testFile, FileMode.Open))
            using (FileStream outstr = new FileStream("TestMultipart", FileMode.Append))
                while ((read = str.Read(buffer, 0, buffer.Length)) > 0)
                    outstr.Write(buffer, 0, read);

                File.AppendAllText("TestMultipart", @"
" + boundary + @"
Content-Disposition: form-data; name=""file2""; filename=""testFile2.test""
Content-Type: application/octet-stream

");
            using (FileStream str = new FileStream(testFile, FileMode.Open))
            using (FileStream outstr = new FileStream("TestMultipart", FileMode.Append))
                while ((read = str.Read(buffer, 0, buffer.Length)) > 0)
                    outstr.Write(buffer, 0, read);
            File.AppendAllText("TestMultipart", "\r\n" + boundary);
        }

        [TestMethod]
        public void TestMultipartTestFileStreaming()
        {
            List<MultiPartSection> sections = new List<MultiPartSection>();

            //Input Stream (Normally NetworkStream)
            using (FileStream stream = new FileStream("TestMultipart", FileMode.Open))
            //MultiPartStream For reading sections in-stream
            using (MultiPartStream mStream = new MultiPartStream(stream))
            //Files to write to
            using (FileStream f1 = new FileStream("File1.txt", FileMode.Create))
            using (FileStream f2 = new FileStream("File2.txt", FileMode.Create))
            {

                //Reads a section and handles found file blocks
                MultiPartSection section;
                while ((section = mStream.ReadSection((sect, data, length) =>
                {
                    //Section with name file1
                    if (sect.Name == "file1")
                        f1.Write(data, 0, (int)length);
                    //Section with name file2
                    if (sect.Name == "file2")
                        f2.Write(data, 0, (int)length);
                })) != null)
                {

                    //Add it to the section list
                    if (section != null)
                        sections.Add(section);
                }
            }

            foreach (MultiPartSection section in sections)
            {
                Console.WriteLine("New Section");
                Console.WriteLine($"FileName: {section.FileName}");
                Console.WriteLine($"Name: {section.Name}");
                Console.WriteLine($"Content-Type: {section.ContentType}");
                Console.WriteLine($"Content: {((!section.Streamed) ? section.DataAsString : "")}");
                Console.WriteLine($"Streamed: {section.Streamed}");
                Console.WriteLine("-------------------");
            }
        }

        [TestMethod]
        public void TestMultipartStreaming()
        {
            List<MultiPartSection> sections = new List<MultiPartSection>();
            
            //Input Stream (Normally NetworkStream)
            using (MemoryStream stream = new MemoryStream(exampleBytes))
            //MultiPartStream For reading sections in-stream
            using (MultiPartStream mStream = new MultiPartStream(stream))
            //Files to write to
            using (FileStream f1 = new FileStream("File1.txt", FileMode.Create))
            using (FileStream f2 = new FileStream("File2.txt", FileMode.Create))
            {

                //Reads a section and handles found file blocks
                MultiPartSection section;
                while ((section = mStream.ReadSection((sect, data, length) =>
                 {
                     //Section with name file1
                     if (sect.Name == "file1")
                          f1.Write(data, 0, (int)length);
                     //Section with name file2
                     if (sect.Name == "file2")
                          f2.Write(data, 0, (int)length);
                 })) != null)
                {

                    //Add it to the section list
                    if (section != null)
                        sections.Add(section);
                }
            }

            foreach (MultiPartSection section in sections)
            {
                Console.WriteLine("New Section");
                Console.WriteLine($"FileName: {section.FileName}");
                Console.WriteLine($"Name: {section.Name}");
                Console.WriteLine($"Content-Type: {section.ContentType}");
                Console.WriteLine($"Content: {((!section.Streamed) ? section.DataAsString : "")}");
                Console.WriteLine($"Streamed: {section.Streamed}");
                Console.WriteLine("-------------------");
            }
        }

        [TestMethod]
        public void TestMultiPartStreamingSpeed()
        {
            for (int i = 0; i < 10000; i++)
                TestMultipartStreaming();
        }
    }
}

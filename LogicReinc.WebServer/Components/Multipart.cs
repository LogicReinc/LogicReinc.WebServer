using LogicReinc.Extensions;
using LogicReinc.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogicReinc.WebServer.Components
{
    
    public class MultiPartStream : IDisposable
    {
        static byte[] newLineBytes = Encoding.UTF8.GetBytes("\r\n");
        Stream stream;
        PatternizedStream pStr;
        byte[] splitter;

        public MultiPartStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException();
            this.stream = stream;
            splitter = stream.ReadTill(newLineBytes);
            byte[] fileEnd = new byte[splitter.Length + newLineBytes.Length];
            newLineBytes.CopyTo(fileEnd, 0);
            splitter.CopyTo(fileEnd, newLineBytes.Length);
            pStr = new PatternizedStream(stream, fileEnd);
        }


        public MultiPartSection ReadSection(Action<MultiPartSection, byte[], long> fileHandler)
        {
            return MultiPartSection.ParseStreaming(splitter, pStr, fileHandler);
        }

        public List<MultiPartSection> ReadAllSections(Action<MultiPartSection, byte[], long> fileHandler)
        {
            List<MultiPartSection>sections = new List<MultiPartSection>();
            MultiPartSection section;
            while ((section = ReadSection(fileHandler)) != null)
            {
                if (section != null)
                    sections.Add(section);
            }

            return sections;
        }

        public void Dispose()
        {
            stream.Dispose();
            pStr.Dispose();
        }
    }

    public class MultiPart
    {
        static byte[] newLineBytes = Encoding.UTF8.GetBytes("\r\n");
        public List<MultiPartSection> Sections { get; set; } = new List<MultiPartSection>();
        
        public static MultiPart Parse(Stream stream)
        {
            byte[] bytes = stream.ReadToEnd();

            int splitterEndIndex = bytes.FindSequence(newLineBytes);
            if(splitterEndIndex > -1)
            {
                MultiPart mp = new MultiPart();

                byte[] splitter = new byte[splitterEndIndex];
                Array.Copy(bytes, 0, splitter, 0, splitterEndIndex);


                List<int> sectionIndexes = new List<int>();
                int lastSplitter = 0;
                int cIndex = 0;
                while ((cIndex = bytes.FindSequence(splitter, lastSplitter)) > -1)
                {
                    cIndex += splitter.Length;
                    sectionIndexes.Add(cIndex);
                    lastSplitter = cIndex;
                }

                byte[] partBuffer;
                for (int i = 0; i < sectionIndexes.Count; i++)
                {
                    int length = 0;
                    int index = sectionIndexes[i];
                    if (i == sectionIndexes.Count - 1)
                        length = (int)(stream.Length - index);
                    else
                        length = sectionIndexes[i + 1] - index - splitter.Length;

                    partBuffer = new byte[length];
                    Buffer.BlockCopy(bytes, index, partBuffer, 0, length);
                    MultiPartSection section = MultiPartSection.Parse(partBuffer);

                    if(section != null)
                        mp.Sections.Add(section);
                }

                return mp;
            }

            return null;
        }


    }


    public class MultiPartSection
    {
        static byte[] newLineBytes = Encoding.UTF8.GetBytes("\r\n");
        static byte[] fileSplitter = Encoding.UTF8.GetBytes("\r\n\r\n");
        public string Name { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Data { get; set; }
        public string DataAsString => Encoding.UTF8.GetString(Data);
        public bool Streamed { get; set; }


        public static MultiPartSection ParseStreaming(byte[] splitter, PatternizedStream stream, Action<MultiPartSection, byte[], long> fileHandler)
        {

            byte[] header = null;
            
            //Build Header
            header = stream.ReadTill(fileSplitter);
            if (header.Length == 0)
                return null;
            string headerText = Encoding.UTF8.GetString(header);
            if (headerText == "--")
                return null;
            //Parse Header
            Regex re = new Regex(@"(?<=Content\-Type:)(.*?)(?=$)");
            Match contentTypeMatch = re.Match(headerText);
            re = new Regex(@"(?<=filename\=\"")(.*?)(?=\"")");
            Match fileNameMatch = re.Match(headerText);
            re = new Regex(@"(?<=name\=\"")(.*?)(?=\"")");
            Match nameMatch = re.Match(headerText);

            MultiPartSection section = new MultiPartSection();
            if (contentTypeMatch.Success)
                section.ContentType = contentTypeMatch.Value.Trim();
            if (fileNameMatch.Success)
                section.FileName = fileNameMatch.Value.Trim();
            if (nameMatch.Success)
                section.Name = nameMatch.Value.Trim();

            //Read Content
            byte[] fileEnd = new byte[splitter.Length + newLineBytes.Length];
            newLineBytes.CopyTo(fileEnd, 0);
            splitter.CopyTo(fileEnd, newLineBytes.Length);

            //Files
            if (!string.IsNullOrEmpty(section.FileName) || !string.IsNullOrEmpty(section.ContentType))
            {
                section.Streamed = true;
                long read = 0;
                byte[] buffer = new byte[4096];
                bool isEnd = false;

                while((read = stream.ReadTill(buffer, 0, buffer.Length, out isEnd)) > 0)
                {
                    fileHandler(section, buffer, read);
                    if (isEnd)
                        break;
                }
            }
            //Parameters
            else
                section.Data = stream.ReadTill(fileEnd);

            return section;
        }

        public static MultiPartSection Parse(byte[] part)
        {
            MultiPartSection section = new MultiPartSection();

            int fileStart = part.FindSequence(fileSplitter);
            if (fileStart == -1)
                return null;
            byte[] header = new byte[fileStart];

            Array.Copy(part, header, fileStart);
            string headerTxt = Encoding.UTF8.GetString(header);

            Regex re = new Regex(@"(?<=Content\-Type:)(.*?)");
            Match contentTypeMatch = re.Match(headerTxt);
            re = new Regex(@"(?<=filename\=\"")(.*?)(?=\"")");
            Match fileNameMatch = re.Match(headerTxt);
            re = new Regex(@"(?<=name\=\"")(.*?)(?=\"")");
            Match nameMatch = re.Match(headerTxt);

            if (contentTypeMatch.Success)
                section.ContentType = contentTypeMatch.Value.Trim();
            if (fileNameMatch.Success)
                section.FileName = fileNameMatch.Value.Trim();
            if (nameMatch.Success)
                section.Name = nameMatch.Value.Trim();

            int dataSize = part.Length - header.Length - fileSplitter.Length - newLineBytes.Length;
            section.Data = new byte[dataSize];

            Buffer.BlockCopy(part, fileStart + fileSplitter.Length, section.Data, 0, dataSize);
            return section;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HintPics
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("usage: HintPics script.rpy");
                return 1;
            }

            return new Program().Run(args[0]);
        }

        private static readonly Regex rxImage = new Regex(
            @"image (?<Name>[^ ]+) = ""(?<Path>[^""]+)""",
            RegexOptions.Compiled);

        private static readonly Regex rxScreen = new Regex(
            @"screen (?<Name>[^ ]+):",
            RegexOptions.Compiled);

        private readonly Dictionary<string, Image> images = new Dictionary<string, Image>();



        private int Run(string scriptFile)
        {
            string gameDir = Path.GetDirectoryName(Path.GetFullPath(scriptFile)) ?? @"\";
            Directory.SetCurrentDirectory(gameDir);

            ReadImages();

            FindScreens("scripts");

            return 0;
        }

        private void FindScreens(string dir)
        {
            foreach (string directory in Directory.GetDirectories(dir))
                FindScreens(directory);

            foreach (string file in Directory.GetFiles(dir, "*.rpy"))
                ScanFile(file);
        }

        private void ScanFile(string file)
        {
            Image bitmap = null;
            Graphics G = null;

            try
            {
                using (TextReader textReader = new StreamReader(file))
                {
                    for (string line = textReader.ReadLine()?.Trim(); line != null; line = textReader.ReadLine()?.Trim())
                    {
                        if (line == "")
                            continue;

                        Match M = rxScreen.Match(line);
                        if (!M.Success)
                            return;

                        if (!images.TryGetValue(M.Groups["Name"].Value, out Image image))
                            return;

                        bitmap = (Image) image.Clone();
                        G = Graphics.FromImage(bitmap);
                        break;
                    }

                    if (G == null)
                        return;

                    if (!FindImageButtons(G, textReader))
                        return;

                    string outPath = $@"hints/{file}";
                    outPath = Path.ChangeExtension(outPath, ".jpg");
                    G.Dispose();
                    G = null;

                    Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
                    if (bitmap is Bitmap bmp)
                        bmp.Save(outPath, ImageFormat.Jpeg);
                }
            }
            finally
            {
                G?.Dispose();
                bitmap?.Dispose();
            }
        }

        private bool FindImageButtons(Graphics graphics, TextReader textReader)
        {
            return true;
        }

        private void ReadImages()
        {
            using (TextReader textReader = new StreamReader("script.rpy"))
            {
                for (string line = textReader.ReadLine()?.Trim(); line != null; line = textReader.ReadLine()?.Trim())
                {
                    Match M = rxImage.Match(line);
                    if (!M.Success)
                        continue;

                    string name = M.Groups["Name"].Value;
                    string path = M.Groups["Path"].Value.Replace("/", @"\");
                    if (path.StartsWith(@"\"))
                        path = $".{path}";
                    images[name] = Image.FromFile(path);
                }
            }
        }
    }
}

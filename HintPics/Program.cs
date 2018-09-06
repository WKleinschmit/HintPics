using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
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

        private readonly Dictionary<string, string> images = new Dictionary<string, string>();
        private Bitmap bitmap = null;
        private Graphics graphics = null;
        private string screenName = null;
        private int boniFound = 0;

        private int Run(string scriptFile)
        {
            string gameDir = Path.GetDirectoryName(Path.GetFullPath(scriptFile)) ?? @"\";
            Directory.SetCurrentDirectory(gameDir);

            ScanFile("script.rpy", true);
            if (File.Exists("missing.txt"))
                ScanFile("missing.txt", true);
            ScanFile("script.rpy", true);
            FindScreens("scripts", true);
            FindScreens("scripts", false);

            return 0;
        }

        private void FindScreens(string dir, bool imagesOnly)
        {
            foreach (string directory in Directory.GetDirectories(dir))
                FindScreens(directory, imagesOnly);

            foreach (string file in Directory.GetFiles(dir, "*.rpy"))
                ScanFile(file, imagesOnly);
        }

        private void ScanFile(string file, bool imagesOnly)
        {

            using (TextReader textReader = new StreamReader(file))
            {
                for (string line = textReader.ReadLine()?.Trim(); line != null; line = textReader.ReadLine()?.Trim())
                {
                    if (line == "")
                        continue;

                    if (imagesOnly)
                    {
                        Match M1 = rxImage.Match(line);
                        if (M1.Success)
                        {
                            string name = M1.Groups["Name"].Value;
                            string path = M1.Groups["Path"].Value;
                            if (path.StartsWith("/"))
                                path = path.Substring(1);
                            images[name] = path;
                            continue;
                        }

                        continue;
                    }

                    Match M2 = rxScreen.Match(line);
                    if (M2.Success)
                    {
                        SaveCurrentImage();
                        screenName = M2.Groups["Name"].Value;
                        if (images.TryGetValue(screenName, out string imagePath) && File.Exists(imagePath))
                        {
                            bitmap = (Bitmap) Image.FromFile(imagePath);
                            graphics = Graphics.FromImage(bitmap);
                            graphics.PageUnit = GraphicsUnit.Pixel;
                        }                        continue;
                    }

                    if (line == "imagebutton:")
                    {
                        int? xPos = null, yPos = null;
                        string hoverImage = null;
                        int? indent = null;
                        while (true)
                        {
                            line = textReader.ReadLine();
                            if (line == null)
                                break;
                            if (line == "")
                                continue;

                            if (indent.HasValue)
                            {
                                if (indent.Value > line.TakeWhile(c => c == ' ').Count())
                                    break;
                            }
                            else
                                indent = line.TakeWhile(c => c == ' ').Count();

                            string[] parts = line.Split(" ".ToCharArray(), 2, StringSplitOptions.RemoveEmptyEntries);
                            switch (parts[0])
                            {
                                case "xpos":
                                    if (int.TryParse(parts[1], out int x))
                                        xPos = x;
                                    break;
                                case "ypos":
                                    if (int.TryParse(parts[1], out int y))
                                        yPos = y;
                                    break;
                                case "hover":
                                    hoverImage = parts[1].Trim('\"', '/');
                                    if (!File.Exists(hoverImage))
                                        hoverImage = null;
                                    break;
                            }
                        }

                        if (xPos.HasValue && yPos.HasValue && hoverImage != null)
                        {
                            if (!hoverImage.Contains("/Bonus/"))
                                continue;

                            if (graphics == null)
                            {
                                File.AppendAllText("missing.txt", $"image {screenName} = \"images/\"\n");
                                continue;
                            }
                            using (Bitmap hover = (Bitmap) Image.FromFile(hoverImage))
                            {
                                Rectangle rc = new Rectangle(xPos.Value, yPos.Value, hover.Width, hover.Height);
                                rc.Inflate(3, 3);
                                rc.Offset(1,1);

                                using (Brush br = new SolidBrush(Color.Red))
                                    graphics.FillRectangle(br, rc);
                                using (Pen pn = new Pen(Color.DarkGoldenrod, 2))
                                    graphics.DrawRectangle(pn, rc);
                                graphics.DrawImageUnscaled(hover, xPos.Value, yPos.Value);
                                ++boniFound;
                            }
                        }
                    }
                }
                SaveCurrentImage();
            }
        }

        private void SaveCurrentImage()
        {
            if (bitmap == null)
                return;

            string outPath = $@"hints/{screenName}";
            outPath = Path.ChangeExtension(outPath, ".png");

            graphics.Dispose();
            graphics = null;

            if (boniFound > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath) ?? ".");
                bitmap.Save(outPath, ImageFormat.Png);
            }

            bitmap.Dispose();
            bitmap = null;
            boniFound = 0;
        }

        private bool FindImageButtons(Graphics graphics, TextReader textReader)
        {
            bool found = false;
            for (string line = textReader.ReadLine()?.Trim(); line != null; line = textReader.ReadLine()?.Trim())
            {
                if (line == "imagebutton:")
                    found |= ParseImageButton(graphics, textReader);
            }
            return found;
        }

        private bool ParseImageButton(Graphics graphics, TextReader textReader)
        {
            string line = textReader.ReadLine()?.Trim();
            if (line == null || !line.StartsWith("xpos ") || !int.TryParse(line.Substring(5), out int xPos))
                return false;

            line = textReader.ReadLine()?.Trim();
            if (line == null || !line.StartsWith("ypos ") || !int.TryParse(line.Substring(5), out int yPos))
                return false;

            line = textReader.ReadLine()?.Trim();
            if (line == null || !line.StartsWith("focus_mask "))
                return false;

            line = textReader.ReadLine()?.Trim();
            if (line == null || !line.StartsWith("idle "))
                return false;
            string idleImagePath = line.Substring(5).Trim(" \t\"".ToCharArray());

            line = textReader.ReadLine()?.Trim();
            if (line == null || !line.StartsWith("hover "))
                return false;
            string hoverImagePath = line.Substring(6).Trim(" \t\"".ToCharArray());

            if (!hoverImagePath.Contains("/Bonus/"))
                return false;

            if (!(Image.FromFile(hoverImagePath) is Bitmap hoverImage))
                return false;

            Rectangle rc = new Rectangle(xPos, yPos, hoverImage.Width, hoverImage.Height);
            rc.Inflate(2, 2);

            using (Brush br = new SolidBrush(Color.Red))
                graphics.FillRectangle(br, rc);

            graphics.DrawImageUnscaled(hoverImage, xPos, yPos);


            return true;
        }
    }
}

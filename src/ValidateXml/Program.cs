using ArgumentException = System.ArgumentException;
using Environment = System.Environment;
using Console = System.Console;
using FileInfo = System.IO.FileInfo;
using System.Collections.Generic;
using Path = System.IO.Path;
using DirectoryInfo = System.IO.DirectoryInfo;
using Directory = System.IO.Directory;
using System.Linq;

// We can not cherry-pick imports from System.CommandLine since InvokeAsync is a necessary extension.
using System.CommandLine;
using System.Xml;
using System.Xml.Schema;

namespace ValidateXml
{
    class Program
    {
        protected static XmlSchemaSet xmlSchemaSet = new XmlSchemaSet();
        protected static List<string> xmlInputs = new List<string>();

        private static bool ValidateInputs()
        {
            // needs to get refactored with EnumerateFiles function and search pattern like *.xml - looks not right
            string cwd = System.IO.Directory.GetCurrentDirectory();
            var valid = xmlInputs.Count() > 0;

            foreach (string pattern in xmlInputs)
            {
                IEnumerable<string> paths;
                if (Path.IsPathRooted(pattern))
                {
                    var root = Path.GetPathRoot(pattern);
                    if (root == null)
                    {
                        throw new ArgumentException(
                            $"Root could not be retrieved from rooted pattern: {pattern}");
                    }

                    var relPattern = Path.GetRelativePath(root, pattern);
                    paths = GlobExpressions.Glob.Files(root, relPattern)
                        .Select((path) => Path.Join(root, relPattern));
                }
                else
                {
                    paths = GlobExpressions.Glob.Files(cwd, pattern)
                        .Select((path) => Path.Join(cwd, path));
                }

                foreach (string path in paths)
                {
                    XmlReaderSettings settings = new XmlReaderSettings();
                    settings.ValidationType = ValidationType.Schema;
                    settings.Schemas = xmlSchemaSet;

                    var messages = new List<string>();
                    settings.ValidationEventHandler += (object sender, ValidationEventArgs e) =>
                    {
                        messages.Add(e.Message);
                    };

                    XmlReader reader = XmlReader.Create(path, settings);

                    while (reader.Read())
                    {
                        // Invoke callbacks
                    };

                    if (messages.Count > 0)
                    {
                        Console.Error.WriteLine($"FAIL: {path}");
                        foreach (string message in messages)
                        {
                            Console.Error.WriteLine(message);
                        }

                        valid = valid & false;
                    }
                    else
                    {
                        Console.WriteLine($"OK: {path}");
                        valid = valid & true;
                    }
                }
            }

            return valid;
        }

        private static bool LoadXMLSchemaFromFileInfo(FileInfo schemaFileInfo)
        {
            return LoadXMLSchemaFromFileFullname(schemaFileInfo.FullName);
        }

        private static bool LoadXMLSchemaFromFileFullname(string fileFullName)
        {
            xmlSchemaSet.Add(null, fileFullName);

            var schemaMessages = new List<string>();
            xmlSchemaSet.ValidationEventHandler += (object sender, ValidationEventArgs e) =>
            {
                schemaMessages.Add(e.Message);
            };
            xmlSchemaSet.Compile();

            if (schemaMessages.Count > 0)
            {
                Console.Error.WriteLine($"Failed to compile the schema: {fileFullName}");
                foreach (string message in schemaMessages)
                {
                    Console.Error.WriteLine(message);
                    return false;
                }
            }
            return true;
        }

        private static int Handle(string[] inputs, FileInfo schema, DirectoryInfo schemes)
        {
            xmlInputs = inputs.ToList();
            var valid = (schema != null) || (schemes != null);

            if(schema != null)
            {
                valid = valid & LoadXMLSchemaFromFileInfo(schema);
            }

            if (schemes != null)
            {
                valid = schemes.EnumerateFiles("*.xsd").Count() > 0;
                foreach (var xsdFile in schemes.EnumerateFiles("*.xsd"))
                {
                    valid = valid & LoadXMLSchemaFromFileFullname(xsdFile.FullName);
                }
            }

            if(valid)
            {
                return (ValidateInputs()) ? 0 : 1;
            }
        
            return 1;
        }

        private static int MainWithCode(string[] args)
        {
            var rootCommand = new RootCommand("Validates the XML files with the given XSD file or the given XSD files from a directory.")
            {
                new Option<string[]>(
                        new[] {"--inputs", "-i"},
                        "Glob patterns of the files to be validated")
                    {Required = true},

                new Option<FileInfo>(
                    new[] {"--schema", "-f"},
                    "Path to the XSD schema file")
                {
                    Required = false,
                    Argument = new Argument<FileInfo>().ExistingOnly()
                },

                new Option<DirectoryInfo>(
                    new[] { "--schemes", "-d"},
                    "Path to the XSD schema folder")
                {
                    Required = false,
                    Argument = new Argument<DirectoryInfo>().ExistingOnly()
                }
            };

            rootCommand.Handler = System.CommandLine.Invocation.CommandHandler.Create(
                (string[] inputs, FileInfo schema, DirectoryInfo schemes) => Handle(inputs, schema, schemes));

            return rootCommand.InvokeAsync(args).Result;
        }

        public static void Main(string[] args)
        {
            xmlSchemaSet.XmlResolver = new XmlUrlResolver();
            int exitCode = MainWithCode(args);
            Environment.ExitCode = exitCode;
        }
    }
}
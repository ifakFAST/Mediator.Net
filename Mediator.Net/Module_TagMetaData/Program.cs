// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.TagMetaData;

class Program
{
    static void Main(string[] args) {

        //const string file = @"D:\Projekte\Jacobs\TagMetadata\MetaDataTables.xlsx";
        //using System.IO.FileStream fs = new(file, FileMode.Open, FileAccess.Read);
        //MetaModel model = MetaModelExcel.ImportFromExcel(fs);
        //model.Validate();
        //string xml = Util.Xml.ToXml(model);
        //System.IO.File.WriteAllText(@"D:\Projekte\Jacobs\TagMetadata\MetaModel.xml", xml);

        //TagMetaData_Model m = TagMetaData_Model.CreateExample();
        //string xml = Util.Xml.ToXml(m);
        //System.IO.File.WriteAllText(@"D:\Projekte\Jacobs\TagMetadata\Model_TagMetaData.xml", xml);

        if (args.Length < 1) {
            Console.Error.WriteLine("Missing argument: port");
            return;
        }

        int port = int.Parse(args[0]);

        // Required to suppress premature shutdown when
        // pressing CTRL+C in parent Mediator console window:
        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) {
            e.Cancel = true;
        };

        var module = new Module();
        ExternalModuleHost.ConnectAndRunModule(port, module);
        Console.WriteLine("Terminated.");
    }
}
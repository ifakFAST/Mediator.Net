using System.Collections.Generic;

namespace Ifak.Fast.Mediator.IO
{
    public class Adapter
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public string Address { get; set; }

        public Login? Login { get; set; }

        public List<NamedValue> Config { get; set; }

        public List<Node> Nodes { get; set; }

        public List<DataItem> DataItems { get; set; }

        public List<DataItem> GetAllDataItems() {
            var res = new List<DataItem>();
            foreach (Node n in Nodes)
                res.AddRange(n.GetAllDataItems());
            res.AddRange(DataItems);
            return res;
        }
    }

    public class Node
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public List<NamedValue> Config { get; set; }

        public List<Node> Nodes { get; set; }

        public List<DataItem> DataItems { get; set; }

        public List<DataItem> GetAllDataItems() {
            var res = new List<DataItem>();
            foreach (Node n in Nodes)
                res.AddRange(n.GetAllDataItems());
            res.AddRange(DataItems);
            return res;
        }
    }

    public class DataItem
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Unit { get; set; }

        public DataType Type { get; set; }

        public string TypeConstraints { get; set; }

        public int Dimension { get; set; }

        public string[] DimensionNames { get; set; }

        public bool Read { get; set; }

        public bool Write { get; set; }

        public string Address { get; set; }

        public List<NamedValue> Config { get; set; }

        public DataValue GetDefaultValue() => DataValue.FromDataType(Type, Dimension);
    }

    public struct Login
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }
}

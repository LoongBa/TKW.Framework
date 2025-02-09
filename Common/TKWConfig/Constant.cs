namespace TKW.Framework.Common.TKWConfig
{
    public class Constant
    {

        public Constant()
        {
        }
        public Constant(Constant constant) : this(constant.Name, constant.Value, constant.Text)
        {
        }

        public Constant(string name, string value) : this(name, value, "")
        {
        }

        public Constant(string name, string value, string text = "")
        {
            Name = name;
            Value = value;
            Text = text;
        }

        public string Name { get; set; }

        public string Value { get; set; }

        public string Text { get; set; }
    }
}
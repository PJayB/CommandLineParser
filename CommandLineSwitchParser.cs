using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections;

namespace FissionBuildAndDeploy
{
    class CommandLineSwitchParser
    {
        public CommandLineSwitchParser()
        {
        }

        private bool TrySetParse<T>(T output, FieldInfo field, Type type, string value)
        {
            if (type.IsEnum)
            {
                field.SetValue(output, Enum.Parse(type, value));
                return true;
            }
            else 
            {
                MethodInfo parseFunc = null;

                parseFunc = type.GetMethod("Parse", new Type[] { typeof(string)});
                if (parseFunc == null)
                    return false;

                field.SetValue(output, parseFunc.Invoke(output, new object[] { value }));
                return true;
            }
        }

        private void SetValue<T>(T output, FieldInfo field, Type type, string value)
        {
            if (!TrySetParse(output, field, type, value))
            {
                // Hope for the best!
                field.SetValue(output, value);
            }
        }

        delegate T ParseDelegate<T>(string s);

        private IList ConvertStringList(IList<string> strings, Type elementType)
        {
            if (elementType == typeof(string))
                return strings as IList;

            MethodInfo parseFunc = elementType.GetMethod("Parse", new Type[] { typeof(string) });
            IList list = (IList)Array.CreateInstance(elementType, strings.Count);
            if (parseFunc != null)
            {
                for (int i = 0; i < strings.Count; ++i)
                {
                    list[i] = parseFunc.Invoke(null, new object[] { strings[i] });
                }
            }
            else
            {
                for (int i = 0; i < strings.Count; ++i)
                {
                    list[i] = Activator.CreateInstance(elementType, strings[i]); ;
                }
            }
            return list;
        }

        private bool HasEnumerableConstructor(Type type)
        {
            return 
                type != typeof(string) && 
                type.GetInterfaces().Any(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        private void SetValues<T>(T output, FieldInfo field, Type elementType, IList<string> values)
        {
            IList objs = ConvertStringList(values, elementType);
            if (field.FieldType.IsArray)
            {
                field.SetValue(output, objs);
            }
            else
            {
                field.SetValue(output, Activator.CreateInstance(field.FieldType, objs));
            }
        }

        public void Parse<T>(string[] args, T output) where T : class
        {
            Type type = typeof(T);

            // Grab a list of properties from T
            Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                fields.Add(field.Name, field);
            }

            for (int i = 0; i < args.Length; ++i)
            {
                if (!args[i].StartsWith("/"))
                {
                    throw new Exception("Invalid switch: " + args[i]);
                }

                string arg = args[i].Substring(1);

                // Find the property in T
                if (!fields.ContainsKey(arg))
                {
                    throw new Exception("Unknown switch: " + arg);
                }

                FieldInfo field = fields[arg];

                // If it's a toggle no need to check for more args
                if (field.FieldType == typeof(bool) || field.FieldType == typeof(Nullable<bool>))
                {
                    field.SetValue(output, true);
                }
                else if (i < args.Length - 1)
                {
                    if (args[i + 1].StartsWith("/"))
                        throw new Exception("Expected: value for " + arg);

                    if (field.FieldType.IsArray || HasEnumerableConstructor(field.FieldType))
                    {
                        List<string> values = new List<string>();
                        while (i < args.Length && !args[i+1].StartsWith("/"))
                        {
                            values.Add(args[++i]);
                        }

                        if (field.FieldType.IsArray)
                            SetValues(output, field, field.FieldType.GetElementType(), values);
                        else
                            SetValues(output, field, field.FieldType.GetGenericArguments()[0], values);
                    }
                    else
                    {
                        SetValue(output, field, field.FieldType, args[++i]);
                    }
                }
            }
        }
    }
}

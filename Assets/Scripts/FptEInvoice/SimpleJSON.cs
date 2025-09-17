/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 *
 * Desc: SimpleJSON is an extremely light-weight JSON parser written in C#.
 * It is designed to be as fast as possible for parsing and does not
 * store any type information or provide any type validation.
 *
 * The goal of SimpleJSON is to make parsing JSON in Unity a joy.
 *
 * The MIT License (MIT)
 * Copyright (c) 2012-2017 tomtom, prime[31]
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SimpleJSON
{
    public enum JSONNodeType
    {
        Array = 1,
        Object = 2,
        String = 3,
        Number = 4,
        Boolean = 5,
        Null = 6
    }

    public enum JSONTextMode
    {
        Compact,
        Indent
    }

    public abstract partial class JSONNode
    {
        #region common interface

        public abstract JSONNodeType Tag { get; }

        public virtual JSONNode this[int aIndex]
        {
            get { return null; }
            set { }
        }

        public virtual JSONNode this[string aKey]
        {
            get { return null; }
            set { }
        }

        public virtual string Value
        {
            get { return ""; }
            set { }
        }

        public virtual int Count
        {
            get { return 0; }
        }

        public virtual bool IsNumber
        {
            get { return false; }
        }

        public virtual bool IsNull // <-- THÊM DÒNG NÀY
        {
            get { return false; }
        }

        public virtual bool IsString
        {
            get { return false; }
        }

        public virtual bool IsBoolean
        {
            get { return false; }
        }

        public virtual bool IsArray
        {
            get { return false; }
        }

        public virtual bool IsObject
        {
            get { return false; }
        }

        public virtual bool Inline
        {
            get { return false; }
            set { }
        }

        public virtual void Add(string aKey, JSONNode aItem)
        {
        }

        public virtual void Add(JSONNode aItem)
        {
            Add("", aItem);
        }

        public virtual void Remove(string aKey)
        {
        }

        public virtual void Remove(int aIndex)
        {
        }

        public virtual void Remove(JSONNode aNode)
        {
        }

        public virtual IEnumerable<JSONNode> Children
        {
            get { yield break; }
        }

        public IEnumerable<JSONNode> DeepChildren
        {
            get
            {
                foreach (var C in Children)
                    foreach (var D in C.DeepChildren)
                        yield return D;
            }
        }

        public override string ToString()
        {
            return SaveToJSON();
        }

        public virtual string ToString(JSONTextMode aMode)
        {
            return SaveToJSON(aMode);
        }

        public abstract string SaveToJSON(JSONTextMode aMode = JSONTextMode.Compact);

        public abstract void SaveToStream(TextWriter aWriter, JSONTextMode aMode = JSONTextMode.Compact);

        public virtual void SaveToCompressedStream(Stream aData, JSONTextMode aMode = JSONTextMode.Compact)
        {
            throw new Exception("Compression is not supported!");
        }

        public virtual void SaveToCompressedFile(string aFileName, JSONTextMode aMode = JSONTextMode.Compact)
        {
            throw new Exception("Compression is not supported!");
        }

        public virtual string SaveToCompressedBase64()
        {
            throw new Exception("Compression is not supported!");
        }

        public static implicit operator JSONNode(string s)
        {
            return new JSONString(s);
        }

        public static implicit operator string(JSONNode d)
        {
            return (d == null) ? null : d.Value;
        }

        public static implicit operator JSONNode(double n)
        {
            return new JSONNumber(n);
        }

        public static implicit operator double(JSONNode d)
        {
            return (d == null) ? 0 : d.AsDouble;
        }

        public static implicit operator JSONNode(float n)
        {
            return new JSONNumber(n);
        }

        public static implicit operator float(JSONNode d)
        {
            return (d == null) ? 0 : d.AsFloat;
        }

        public static implicit operator JSONNode(int n)
        {
            return new JSONNumber(n);
        }

        public static implicit operator int(JSONNode d)
        {
            return (d == null) ? 0 : d.AsInt;
        }

        public static implicit operator JSONNode(long n)
        {
            return new JSONNumber(n);
        }

        public static implicit operator long(JSONNode d)
        {
            return (d == null) ? 0 : d.AsLong;
        }

        public static implicit operator JSONNode(bool b)
        {
            return new JSONBool(b);
        }

        public static implicit operator bool(JSONNode d)
        {
            return d != null && d.AsBool;
        }

        public static implicit operator JSONNode(ArrayList a)
        {
            var obj = new JSONArray();
            foreach (var s in a)
                obj.Add(new JSONString(s.ToString()));
            return obj;
        }

        public static implicit operator JSONNode(Dictionary<string, string> d)
        {
            var obj = new JSONObject();
            foreach (var s in d)
                obj.Add(s.Key, new JSONString(s.Value));
            return obj;
        }

        public static implicit operator JSONNode(byte[] a)
        {
            var obj = new JSONArray();
            foreach (var s in a)
                obj.Add(new JSONNumber(s));
            return obj;
        }

        public static bool operator ==(JSONNode a, object b)
        {
            if (ReferenceEquals(a, b))
                return true;
            bool aIsNull = a is JSONNull || ReferenceEquals(a, null);
            bool bIsNull = b is JSONNull || ReferenceEquals(b, null);
            if (aIsNull && bIsNull)
                return true;
            if (aIsNull || bIsNull)
                return false;
            return a.Equals(b);
        }

        public static bool operator !=(JSONNode a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        internal static StringBuilder Builder = new StringBuilder();
        internal static StringWriter Writer = new StringWriter(Builder);


        internal static void WriteIndent(TextWriter aWriter, int aIndent)
        {
            for (int i = 0; i < aIndent; i++)
                aWriter.Write('\t');
        }

        internal static void WriteLabel(TextWriter aWriter, string aLabel)
        {
            aWriter.Write('\"');
            aWriter.Write(Escape(aLabel));
            aWriter.Write("\":");
        }

        internal static string Escape(string aText)
        {
            var sb = new StringBuilder();
            foreach (char c in aText)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        #endregion common interface

        #region parsing

        protected static JSONNode ParseElement(string Json, ref int offset)
        {
            for (;; )
            {
                char ch = Json[offset];
                switch (ch)
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        offset++;
                        continue;
                    case '{':
                        return JSONObject.Parse(Json, ref offset);
                    case '[':
                        return JSONArray.Parse(Json, ref offset);
                    case '\"':
                        return JSONString.Parse(Json, ref offset);
                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        return JSONNumber.Parse(Json, ref offset);
                    case 't':
                    case 'f':
                        return JSONBool.Parse(Json, ref offset);
                    case 'n':
                        return JSONNull.Parse(Json, ref offset);
                    default:
                        throw new Exception("Invalid character encountered in JSON string: " + ch);
                }
            }
        }

        public static JSONNode Parse(string json)
        {
            int offset = 0;
            return ParseElement(json, ref offset);
        }

        #endregion parsing

        #region typecasting properties

        public virtual double AsDouble
        {
            get
            {
                double v;
                return double.TryParse(Value, out v) ? v : 0.0;
            }
        }

        public virtual float AsFloat
        {
            get
            {
                float v;
                return float.TryParse(Value, out v) ? v : 0.0f;
            }
        }

        public virtual int AsInt
        {
            get
            {
                int v;
                return int.TryParse(Value, out v) ? v : 0;
            }
        }

        public virtual long AsLong
        {
            get
            {
                long v;
                return long.TryParse(Value, out v) ? v : 0L;
            }
        }

        public virtual bool AsBool
        {
            get
            {
                bool v;
                return bool.TryParse(Value, out v) ? v : !string.IsNullOrEmpty(Value);
            }
        }

        public virtual JSONArray AsArray
        {
            get { return this as JSONArray; }
        }

        public virtual JSONObject AsObject
        {
            get { return this as JSONObject; }
        }

        #endregion typecasting properties

        #region enumerators

        public virtual IEnumerator<KeyValuePair<string, JSONNode>> GetEnumerator()
        {
            yield break;
        }

        public IEnumerator GetEnumerator(Type type)
        {
            return GetEnumerator();
        }

        #endregion enumerators
    }
    // End of JSONNode

    public partial class JSONObject : JSONNode
    {
        private Dictionary<string, JSONNode> m_Dict = new Dictionary<string, JSONNode>();
        private bool inline;

        public override bool Inline
        {
            get { return inline; }
            set { inline = value; }
        }

        public override JSONNodeType Tag
        {
            get { return JSONNodeType.Object; }
        }

        public override bool IsObject
        {
            get { return true; }
        }

        public override IEnumerable<JSONNode> Children
        {
            get { return m_Dict.Values; }
        }

        public override int Count
        {
            get { return m_Dict.Count; }
        }

        public override void Add(string aKey, JSONNode aItem)
        {
            if (string.IsNullOrEmpty(aKey))
                aKey = Guid.NewGuid().ToString();
            if (m_Dict.ContainsKey(aKey))
                m_Dict[aKey] = aItem;
            else
                m_Dict.Add(aKey, aItem);
        }

        public override void Remove(string aKey)
        {
            m_Dict.Remove(aKey);
        }

        public override void Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_Dict.Count)
                return;
            m_Dict.Remove(m_Dict.Keys.ElementAt(aIndex));
        }

        public override void Remove(JSONNode aNode)
        {
            try
            {
                string Key = m_Dict.Where(item => item.Value == aNode).Select(item => item.Key).FirstOrDefault();
                if (Key != null)
                    m_Dict.Remove(Key);
                else
                    throw new Exception("Unable to remove non-existing node!");
            }
            catch
            {
                throw new Exception("Unable to remove non-existing node!");
            }
        }

        public override JSONNode this[string aKey]
        {
            get
            {
                if (m_Dict.ContainsKey(aKey))
                    return m_Dict[aKey];
                return new JSONLazyCreator(this, aKey);
            }
            set
            {
                if (m_Dict.ContainsKey(aKey))
                    m_Dict[aKey] = value;
                else
                    m_Dict.Add(aKey, value);
            }
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            Builder.Length = 0;
            SaveToStream(Writer, aMode);
            return Builder.ToString();
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
            bool first = true;
            aWriter.Write('{');
            if (aMode == JSONTextMode.Indent)
                aWriter.Write('\n');

            //if (aMode == JSONTextMode.Indent)
             //   indent = aWriter.IndentLevel();
            //aWriter.IndentLevel(indent + 1);
            foreach (var item in m_Dict)
            {
                if (!first)
                {
                    aWriter.Write(',');
                    if (aMode == JSONTextMode.Indent)
                        aWriter.Write('\n');
                }

                first = false;
                if (aMode == JSONTextMode.Indent)
                     WriteIndent(aWriter, 1);
                WriteLabel(aWriter, item.Key);
                item.Value.SaveToStream(aWriter, aMode);
            }



            aWriter.Write('}');
            //aWriter.IndentLevel(indent);
        }

        public override IEnumerator<KeyValuePair<string, JSONNode>> GetEnumerator()
        {
            return m_Dict.GetEnumerator();
        }

        public static JSONNode Parse(string aJSON, ref int aOffset)
        {
            JSONObject obj = new JSONObject();
            aOffset++;
            bool done = false;
            while (!done)
            {
                char ch = aJSON[aOffset];
                switch (ch)
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        aOffset++;
                        continue;
                    case '}':
                        aOffset++;
                        return obj;
                    case ',':
                        aOffset++;
                        continue;
                    default:
                        string Key = JSONString.Parse(aJSON, ref aOffset);
                        for (ch = aJSON[aOffset]; ch != ':'; ch = aJSON[++aOffset])
                        {
                            if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                                continue;
                            throw new Exception("Expected ':' in JSON string at " + aOffset);
                        }

                        aOffset++; // pass ':'
                        JSONNode Value = ParseElement(aJSON, ref aOffset);
                        obj.Add(Key, Value);
                        break;
                }
            }

            return obj;
        }
    }
    // End of JSONObject

    public partial class JSONArray : JSONNode
    {
        private List<JSONNode> m_List = new List<JSONNode>();
        private bool inline;

        public override bool Inline
        {
            get { return inline; }
            set { inline = value; }
        }

        public override JSONNodeType Tag
        {
            get { return JSONNodeType.Array; }
        }

        public override bool IsArray
        {
            get { return true; }
        }

        public override IEnumerable<JSONNode> Children
        {
            get { return m_List; }
        }

        public override int Count
        {
            get { return m_List.Count; }
        }

        public override void Add(string aKey, JSONNode aItem)
        {
            m_List.Add(aItem);
        }

        public override void Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_List.Count)
                return;
            m_List.RemoveAt(aIndex);
        }

        public override void Remove(JSONNode aNode)
        {
            m_List.Remove(aNode);
        }

        public override JSONNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_List.Count)
                    return new JSONLazyCreator(this);
                return m_List[aIndex];
            }
            set
            {
                if (aIndex < 0 || aIndex >= m_List.Count)
                    m_List.Add(value);
                else
                    m_List[aIndex] = value;
            }
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            Builder.Length = 0;
            SaveToStream(Writer, aMode);
            return Builder.ToString();
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
            bool first = true;
            aWriter.Write('[');
            if (aMode == JSONTextMode.Indent)
                aWriter.Write('\n');

            //if (aMode == JSONTextMode.Indent)
            //    indent = aWriter.IndentLevel();
            //aWriter.IndentLevel(indent + 1);
            foreach (var item in m_List)
            {
                if (!first)
                {
                    aWriter.Write(',');
                    if (aMode == JSONTextMode.Indent)
                        aWriter.Write('\n');
                }

                first = false;
                if (aMode == JSONTextMode.Indent)
                     WriteIndent(aWriter, 1);
                item.SaveToStream(aWriter, aMode);
            }



            aWriter.Write(']');
            //aWriter.IndentLevel(indent);
        }

        public override IEnumerator<KeyValuePair<string, JSONNode>> GetEnumerator()
        {
            for (int i = 0; i < m_List.Count; i++)
                yield return new KeyValuePair<string, JSONNode>(i.ToString(), m_List[i]);
        }

        public static JSONNode Parse(string aJSON, ref int aOffset)
        {
            JSONArray arr = new JSONArray();
            aOffset++;
            bool done = false;
            while (!done)
            {
                char ch = aJSON[aOffset];
                switch (ch)
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        aOffset++;
                        continue;
                    case ']':
                        aOffset++;
                        return arr;
                    case ',':
                        aOffset++;
                        continue;
                    default:
                        arr.Add(ParseElement(aJSON, ref aOffset));
                        break;
                }
            }

            return arr;
        }
    }
    // End of JSONArray

    public partial class JSONString : JSONNode
    {
        private string m_Data;

        public override JSONNodeType Tag
        {
            get { return JSONNodeType.String; }
        }

        public override bool IsString
        {
            get { return true; }
        }

        public override string Value
        {
            get { return m_Data; }
            set { m_Data = value; }
        }

        public JSONString(string aData)
        {
            m_Data = aData;
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            Builder.Length = 0;
            SaveToStream(Writer, aMode);
            return Builder.ToString();
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
            aWriter.Write('\"');
            aWriter.Write(Escape(m_Data));
            aWriter.Write('\"');
        }

        public static JSONNode Parse(string aJSON, ref int aOffset)
        {
            aOffset++;
            int start = aOffset;
            for (; aOffset < aJSON.Length; aOffset++)
            {
                char ch = aJSON[aOffset];
                if (ch == '\\')
                {
                    aOffset++;
                    continue;
                }

                if (ch == '"')
                {
                    string tmp = aJSON.Substring(start, aOffset - start);
                    aOffset++;
                    return new JSONString(UnEscape(tmp));
                }
            }

            throw new Exception("Expected '\"' in JSON string at " + aOffset);
        }

        private static string UnEscape(string aText)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < aText.Length; i++)
            {
                char ch = aText[i];
                if (ch == '\\')
                {
                    i++;
                    ch = aText[i];
                    switch (ch)
                    {
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '\"':
                            sb.Append('\"');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case 'b':
                            sb.Append('\b');
                            break;
                        case 'f':
                            sb.Append('\f');
                            break;
                        case 'u':
                            string hex = aText.Substring(i + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            i += 4;
                            break;
                        default:
                            sb.Append(ch);
                            break;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }
    // End of JSONString

    public partial class JSONNumber : JSONNode
    {
        private double m_Data;

        public override JSONNodeType Tag
        {
            get { return JSONNodeType.Number; }
        }

        public override bool IsNumber
        {
            get { return true; }
        }

        public override string Value
        {
            get { return m_Data.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            set
            {
                double v;
                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out v))
                    m_Data = v;
            }
        }

        public override double AsDouble
        {
            get { return m_Data; }
        }

        public override float AsFloat
        {
            get { return (float)m_Data; }
        }

        public override int AsInt
        {
            get { return (int)m_Data; }
        }

        public override long AsLong
        {
            get { return (long)m_Data; }
        }

        public JSONNumber(double aData)
        {
            m_Data = aData;
        }

        public JSONNumber(string aData)
        {
            Value = aData;
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            Builder.Length = 0;
            SaveToStream(Writer, aMode);
            return Builder.ToString();
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
            aWriter.Write(Value);
        }

        public static JSONNode Parse(string aJSON, ref int aOffset)
        {
            int start = aOffset;
            for (; aOffset < aJSON.Length; aOffset++)
            {
                char ch = aJSON[aOffset];
                if (ch >= '0' && ch <= '9' || ch == '.' || ch == '-' || ch == '+' || ch == 'e' || ch == 'E')
                    continue;
                break;
            }

            string tmp = aJSON.Substring(start, aOffset - start);
            return new JSONNumber(tmp);
        }
    }
    // End of JSONNumber

    public partial class JSONBool : JSONNode
    {
        private bool m_Data;

        public override JSONNodeType Tag
        {
            get { return JSONNodeType.Boolean; }
        }

        public override bool IsBoolean
        {
            get { return true; }
        }

        public override string Value
        {
            get { return m_Data.ToString(); }
            set
            {
                bool v;
                if (bool.TryParse(value, out v))
                    m_Data = v;
            }
        }

        public override bool AsBool
        {
            get { return m_Data; }
        }

        public JSONBool(bool aData)
        {
            m_Data = aData;
        }

        public JSONBool(string aData)
        {
            Value = aData;
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            Builder.Length = 0;
            SaveToStream(Writer, aMode);
            return Builder.ToString();
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
            aWriter.Write(m_Data ? "true" : "false");
        }

        public static JSONNode Parse(string aJSON, ref int aOffset)
        {
            if (aOffset + 4 <= aJSON.Length && aJSON.Substring(aOffset, 4).ToLower() == "true")
            {
                aOffset += 4;
                return new JSONBool(true);
            }

            if (aOffset + 5 <= aJSON.Length && aJSON.Substring(aOffset, 5).ToLower() == "false")
            {
                aOffset += 5;
                return new JSONBool(false);
            }

            throw new Exception("Expected 'true' or 'false' in JSON string at " + aOffset);
        }
    }
    // End of JSONBool

    public partial class JSONNull : JSONNode
    {
        public override JSONNodeType Tag
        {
            get { return JSONNodeType.Null; }
        }

        public override bool IsNull
        {
            get { return true; }
        }

        public override string Value
        {
            get { return "null"; }
            set { }
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            Builder.Length = 0;
            SaveToStream(Writer, aMode);
            return Builder.ToString();
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
            aWriter.Write("null");
        }

        public static JSONNode Parse(string aJSON, ref int aOffset)
        {
            if (aOffset + 4 <= aJSON.Length && aJSON.Substring(aOffset, 4).ToLower() == "null")
            {
                aOffset += 4;
                return new JSONNull();
            }

            throw new Exception("Expected 'null' in JSON string at " + aOffset);
        }
    }
    // End of JSONNull

    internal class JSONLazyCreator : JSONNode
    {
        private JSONNode m_Node = null;
        private string m_Key = null;
        private int m_Index = -1;
        private Action<JSONNode> m_Set;

        public JSONLazyCreator(JSONNode aNode)
        {
            m_Node = aNode;
            m_Set = delegate(JSONNode aItem) { m_Node.Add(aItem); };
        }

        public JSONLazyCreator(JSONNode aNode, string aKey)
        {
            m_Node = aNode;
            m_Key = aKey;
            m_Set = delegate(JSONNode aItem) { m_Node.Add(m_Key, aItem); };
        }

        public JSONLazyCreator(JSONNode aNode, int aIndex)
        {
            m_Node = aNode;
            m_Index = aIndex;
            m_Set = delegate(JSONNode aItem) { m_Node.Add(aItem); };
        }

        public override JSONNodeType Tag
        {
            get { return JSONNodeType.Null; }
        }

        public override JSONNode this[int aIndex]
        {
            get
            {
                var tmp = new JSONArray();
                m_Set(tmp);
                m_Node = tmp;
                return tmp[aIndex];
            }
            set
            {
                var tmp = new JSONArray();
                m_Set(tmp);
                m_Node = tmp;
                tmp[aIndex] = value;
            }
        }

        public override JSONNode this[string aKey]
        {
            get
            {
                var tmp = new JSONObject();
                m_Set(tmp);
                m_Node = tmp;
                return tmp[aKey];
            }
            set
            {
                var tmp = new JSONObject();
                m_Set(tmp);
                m_Node = tmp;
                tmp[aKey] = value;
            }
        }

        public override string Value
        {
            get { return ""; }
            set
            {
                var tmp = new JSONString(value);
                m_Set(tmp);
                m_Node = tmp;
            }
        }

        public override void Add(JSONNode aItem)
        {
            var tmp = new JSONArray();
            m_Set(tmp);
            m_Node = tmp;
            tmp.Add(aItem);
        }

        public override void Add(string aKey, JSONNode aItem)
        {
            var tmp = new JSONObject();
            m_Set(tmp);
            m_Node = tmp;
            tmp.Add(aKey, aItem);
        }

        public static bool operator ==(JSONLazyCreator a, object b)
        {
            if (b is JSONNull)
                return true;
            return ReferenceEquals(a, b);
        }

        public static bool operator !=(JSONLazyCreator a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return obj is JSONNull;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string SaveToJSON(JSONTextMode aMode)
        {
            return "";
        }

        public override void SaveToStream(TextWriter aWriter, JSONTextMode aMode)
        {
        }
    }
    // End of JSONLazyCreator

    public static class JSON
    {
        public static JSONNode Parse(string aJSON)
        {
            return JSONNode.Parse(aJSON);
        }
    }


}
using UnityEngine;

using System;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;

namespace Project
{
  public class JSON
  {
    private const int TOKEN_NONE = 0;
    private const int TOKEN_CURLY_OPEN = 1;
    private const int TOKEN_CURLY_CLOSE = 2;
    private const int TOKEN_SQUARED_OPEN = 3;
    private const int TOKEN_SQUARED_CLOSE = 4;
    private const int TOKEN_COLON = 5;
    private const int TOKEN_COMMA = 6;
    private const int TOKEN_STRING = 7;
    private const int TOKEN_NUMBER = 8;
    private const int TOKEN_TRUE = 9;
    private const int TOKEN_FALSE = 10;
    private const int TOKEN_NULL = 11;

    private const int BUILDER_CAPACITY = 2000;

    public static object FromString(string json)
    {
      bool success = true;

      return FromString(json, ref success);
    }

    public static object FromString(string json, ref bool success)
    {
      success = true;

      if (json != null)
      {
        char[] charArray = json.ToCharArray();

        int index = 0;

        object value = ParseValue(charArray, ref index, ref success);

        return value;
      }
      else
      {
        return null;
      }
    }

    protected static object ParseValue(char[] json, ref int index, ref bool success)
    {
      switch (LookAhead(json, index))
      {
        case JSON.TOKEN_STRING:
          return ParseString(json, ref index, ref success);
        case JSON.TOKEN_NUMBER:
          return ParseNumber(json, ref index, ref success);
        case JSON.TOKEN_CURLY_OPEN:
          return ParseObject(json, ref index, ref success);
        case JSON.TOKEN_SQUARED_OPEN:
          return ParseArray(json, ref index, ref success);
        case JSON.TOKEN_TRUE:
          NextToken(json, ref index);
          return true;
        case JSON.TOKEN_FALSE:
          NextToken(json, ref index);
          return false;
        case JSON.TOKEN_NULL:
          NextToken(json, ref index);
          return null;
        case JSON.TOKEN_NONE:
          break;
      }

      success = false;

      return null;
    }

    protected static Dictionary<string, object> ParseObject(char[] json, ref int index, ref bool success)
    {
      Dictionary<string, object> dict = new Dictionary<string, object>();

      int token;

      // {
      NextToken(json, ref index);

      bool done = false;
      while (!done)
      {
        token = LookAhead(json, index);
        if (token == JSON.TOKEN_NONE)
        {
          success = false;
          return null;
        }
        else if (token == JSON.TOKEN_COMMA)
        {
          NextToken(json, ref index);
        }
        else if (token == JSON.TOKEN_CURLY_CLOSE)
        {
          NextToken(json, ref index);
          return dict;
        }
        else
        {
          // name
          string name = ParseString(json, ref index, ref success);

          if (!success)
          {
            success = false;
            return null;
          }

          // :
          token = NextToken(json, ref index);

          if (token != JSON.TOKEN_COLON)
          {
            success = false;
            return null;
          }

          // value
          object value = ParseValue(json, ref index, ref success);

          if (!success)
          {
            success = false;
            return null;
          }

          //dict[name] = value;
          dict.Add(name, value);
        }
      }

      return dict;
    }

    protected static ArrayList ParseArray(char[] json, ref int index, ref bool success)
    {
      ArrayList array = new ArrayList();

      // [
      NextToken(json, ref index);

      bool done = false;
      while (!done)
      {
        int token = LookAhead(json, index);
        if (token == JSON.TOKEN_NONE)
        {
          success = false;
          return null;
        }
        else if (token == JSON.TOKEN_COMMA)
        {
          NextToken(json, ref index);
        }
        else if (token == JSON.TOKEN_SQUARED_CLOSE)
        {
          NextToken(json, ref index);
          break;
        }
        else
        {
          object value = ParseValue(json, ref index, ref success);
          if (!success)
          {
            return null;
          }

          array.Add(value);
        }
      }

      return array;
    }

    protected static string ParseString(char[] json, ref int index, ref bool success)
    {
      StringBuilder s = new StringBuilder(BUILDER_CAPACITY);

      char c;

      EatWithSpace(json, ref index);

      // "
      c = json[index++];

      bool complete = false;

      while (!complete)
      {

        if (index == json.Length)
        {
          break;
        }

        c = json[index++];
        if (c == '"')
        {
          complete = true;
          break;
        }
        else if (c == '\\')
        {

          if (index == json.Length)
          {
            break;
          }
          c = json[index++];
          if (c == '"')
          {
            s.Append('"');
          }
          else if (c == '\\')
          {
            s.Append('\\');
          }
          else if (c == '/')
          {
            s.Append('/');
          }
          else if (c == 'b')
          {
            s.Append('\b');
          }
          else if (c == 'f')
          {
            s.Append('\f');
          }
          else if (c == 'n')
          {
            s.Append('\n');
          }
          else if (c == 'r')
          {
            s.Append('\r');
          }
          else if (c == 't')
          {
            s.Append('\t');
          }
          else if (c == 'u')
          {
            int remainingLength = json.Length - index;
            if (remainingLength >= 4)
            {
              // parse the 32 bit hex into an integer codepoint
              uint codePoint;
              if (!(success = UInt32.TryParse(new string(json, index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint)))
              {
                return "";
              }
              // convert the integer codepoint to a unicode char and add to string
              s.Append(Char.ConvertFromUtf32((int)codePoint));
              // skip 4 chars
              index += 4;
            }
            else
            {
              break;
            }
          }

        }
        else
        {
          s.Append(c);
        }

      }

      if (!complete)
      {
        success = false;
        return null;
      }

      return s.ToString();
    }

    protected static double ParseNumber(char[] json, ref int index, ref bool success)
    {
      EatWithSpace(json, ref index);

      int lastIndex = GetLastIndexOfNumber(json, index);

      int charLength = (lastIndex - index) + 1;

      double number;

      success = Double.TryParse(new string(json, index, charLength), NumberStyles.Any, CultureInfo.InvariantCulture, out number);

      index = lastIndex + 1;

      return number;
    }

    protected static int GetLastIndexOfNumber(char[] json, int index)
    {
      int lastIndex;

      for (lastIndex = index; lastIndex < json.Length; lastIndex++)
      {
        if ("0123456789+-.eE".IndexOf(json[lastIndex]) == -1)
        {
          break;
        }
      }

      return lastIndex - 1;
    }

    protected static void EatWithSpace(char[] json, ref int index)
    {
      for (; index < json.Length; index++)
      {
        if (" \t\n\r".IndexOf(json[index]) == -1)
        {
          break;
        }
      }
    }

    protected static int LookAhead(char[] json, int index)
    {
      int saveIndex = index;

      return NextToken(json, ref saveIndex);
    }

    protected static int NextToken(char[] json, ref int index)
    {
      EatWithSpace(json, ref index);

      if (index == json.Length)
      {
        return JSON.TOKEN_NONE;
      }

      char c = json[index];

      index++;

      switch (c)
      {
        case '{':
          return JSON.TOKEN_CURLY_OPEN;
        case '}':
          return JSON.TOKEN_CURLY_CLOSE;
        case '[':
          return JSON.TOKEN_SQUARED_OPEN;
        case ']':
          return JSON.TOKEN_SQUARED_CLOSE;
        case ',':
          return JSON.TOKEN_COMMA;
        case '"':
          return JSON.TOKEN_STRING;
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
        case '-':
          return JSON.TOKEN_NUMBER;
        case ':':
          return JSON.TOKEN_COLON;
      }

      index--;

      int remainingLength = json.Length - index;

      // false
      if (remainingLength >= 5)
      {
        if (json[index] == 'f' &&
            json[index + 1] == 'a' &&
            json[index + 2] == 'l' &&
            json[index + 3] == 's' &&
            json[index + 4] == 'e')
        {
          index += 5;
          return JSON.TOKEN_FALSE;
        }
      }

      // true
      if (remainingLength >= 4)
      {
        if (json[index] == 't' &&
            json[index + 1] == 'r' &&
            json[index + 2] == 'u' &&
            json[index + 3] == 'e')
        {
          index += 4;
          return JSON.TOKEN_TRUE;
        }
      }

      // null
      if (remainingLength >= 4)
      {
        if (json[index] == 'n' &&
            json[index + 1] == 'u' &&
            json[index + 2] == 'l' &&
            json[index + 3] == 'l')
        {
          index += 4;
          return JSON.TOKEN_NULL;
        }
      }

      return JSON.TOKEN_NONE;
    }

    // ------------------------------------------------------------------------------------------------------
    public static string ToString(object obj)
    {
      StringBuilder builder = new StringBuilder(BUILDER_CAPACITY);

      bool success = SerializeValue(obj, builder);

      return (success ? builder.ToString() : null);
    }

    protected static bool SerializeValue(object value, StringBuilder builder)
    {
      bool success = true;

      if (value is string)
      {
        success = SerializeString((string)value, builder);
      }
      else if (value is Hashtable)
      {
        success = SerializeHashtable((Hashtable)value, builder);
      }
      else if (value is IList)
      {
        success = SerializeList((IList)value, builder);
      }
      else if (value is Vector2)
      {
        ArrayList arr = new ArrayList();

        Vector2 val = (Vector2)value;

        arr.Add(val.x);
        arr.Add(val.y);

        success = SerializeArray((ArrayList)arr, builder);
      }
      else if (value is ArrayList)
      {
        success = SerializeArray((ArrayList)value, builder);
      }
      else if ((value is Boolean) && ((Boolean)value == true))
      {
        builder.Append("true");
      }
      else if ((value is Boolean) && ((Boolean)value == false))
      {
        builder.Append("false");
      }
      else if (value is ValueType)
      {
        success = SerializeNumber(Convert.ToDouble(value), builder);
      }
      else if (value is object)
      {
        Type tp = value.GetType();

        Hashtable hash = new Hashtable();

        foreach (Attribute attr in tp.GetCustomAttributes(true))
        {
          if (attr is JSONExport)
          {
            PropertyInfo[] props = tp.GetProperties();

            for (int i = 0; i < props.Length; i++)
            {
              PropertyInfo pi = props[i];

              foreach (Attribute attr1 in pi.GetCustomAttributes(true))
              {
                if (attr1 is Export)
                {
                  Export exp = (Export)attr1;

                  hash.Add(exp.name, pi.GetValue(value, null));
                }
              }
            }

            return SerializeHashtable(hash, builder);
          }
        }

        success = SerializeObject(value, builder);
      }
      else if (value == null)
      {
        builder.Append("null");
      }
      else
      {
        success = false;
      }

      return success;
    }

    protected static bool SerializeObject(object obj, StringBuilder builder)
    {
      Hashtable dictionary = new Hashtable();

      PropertyDescriptorCollection props = TypeDescriptor.GetProperties(obj);

      foreach (PropertyDescriptor prop in props)
      {
        object val = prop.GetValue(obj);

        dictionary.Add(prop.Name, val);
      }

      return JSON.SerializeHashtable(dictionary, builder);
    }

    protected static bool SerializeHashtable(Hashtable anObject, StringBuilder builder)
    {
      builder.Append("{");

      IDictionaryEnumerator e = anObject.GetEnumerator();

      bool first = true;

      while (e.MoveNext())
      {
        string key = e.Key.ToString();
        object value = e.Value;

        if (!first)
        {
          builder.Append(", ");
        }

        SerializeString(key, builder);

        builder.Append(":");

        if (!SerializeValue(value, builder))
        {
          return false;
        }

        first = false;
      }

      builder.Append("}");

      return true;
    }

    protected static bool SerializeArray(ArrayList anArray, StringBuilder builder)
    {
      builder.Append("[");

      bool first = true;
      for (int i = 0; i < anArray.Count; i++)
      {
        object value = anArray[i];

        if (!first)
        {
          builder.Append(", ");
        }

        if (!SerializeValue(value, builder))
        {
          return false;
        }

        first = false;
      }

      builder.Append("]");

      return true;
    }

    protected static bool SerializeList(IList list, StringBuilder builder)
    {
      builder.Append("[");

      bool first = true;

      foreach (object value in list)
      {
        if (!first)
        {
          builder.Append(", ");
        }

        if (!SerializeValue(value, builder))
        {
          return false;
        }

        first = false;
      }

      builder.Append("]");

      return true;
    }

    protected static bool SerializeString(string aString, StringBuilder builder)
    {
      builder.Append("\"");

      char[] charArray = aString.ToCharArray();

      for (int i = 0; i < charArray.Length; i++)
      {
        char c = charArray[i];
        if (c == '"')
        {
          builder.Append("\"");
        }
        else if (c == '\\')
        {
          builder.Append("\\\\");
        }
        else if (c == '\b')
        {
          builder.Append("\\b");
        }
        else if (c == '\f')
        {
          builder.Append("\\f");
        }
        else if (c == '\n')
        {
          builder.Append("\\n");
        }
        else if (c == '\r')
        {
          builder.Append("\\r");
        }
        else if (c == '\t')
        {
          builder.Append("\\t");
        }
        else
        {
          //int codepoint = Convert.ToInt32(c);
          //if ((codepoint >= 32) && (codepoint <= 126)) {
          builder.Append(c);
          //} else {
          //   builder.Append("\\u" + Convert.ToString(codepoint, 16).PadLeft(4, '0'));
          //}
        }
      }

      builder.Append("\"");

      return true;
    }

    protected static bool SerializeNumber(double number, StringBuilder builder)
    {
      builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture));

      return true;
    }
  }
}

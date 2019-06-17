using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace KFN_Viewer
{
    class Slogi
    {
        private static readonly HashSet<char> Glas = new HashSet<char>("аоуиэыяюеё"),
                                              Zvonk = new HashSet<char>("лмнр"),
                                              Soglas = new HashSet<char>("бвгджзклмнпрстфхцчшщъь"),
                                              Gluh = new HashSet<char>("бвгджзкпстфхцчшщъь");

        public string Text2Slogs(string text)
        {
            string outputText = "";
            char[] symbols = text.ToCharArray();
            string word = "";
            foreach (char symbol in symbols)
            {
                string ss = Convert.ToString(symbol);
                // eng to cyr
                ss = this.EngToCyr(ss);
                if (Regex.IsMatch(ss, @"[А-Яа-я]"))
                {
                    word += ss;
                }
                else
                {
                    if (word.Length > 0)
                    {
                        IEnumerable<string> slogs = this.Word2Slogs(word.ToLower());
                        foreach (string slog in slogs)
                        {
                            outputText += slog;
                        }
                        word = "";
                    }
                    outputText += ss;
                }
            }
            return outputText;
        }

        public IEnumerable<string> Word2Slogs(string word)
        {
            var sb = new StringBuilder();
            Predicate<int> case1 = index => word[index] == 'й' && Soglas.Contains(word[index + 1]);
            Predicate<int> case2 = index => Zvonk.Contains(word[index]) && Gluh.Contains(word[index + 1]);
            int i = 0;
            for (; GlasLeft(word, i) > 1 || sb.Length != 0; i++)
            {
                sb.Append(word[i]);
                if (case1(i) || case2(i) || Glas.Contains(word[i]) && !(case1(i) || case2(i)))
                {
                    yield return sb.Append('-').ToString();
                    sb.Clear();
                }
            }
            yield return word.Substring(i);
        }

        private static int GlasLeft(string input, int i)
        {
            int count = 0;
            for (int j = i; j < input.Length; j++)
                if (Glas.Contains(input[j]))
                    count++;
            return count;
        }

        private string EngToCyr(string symbol)
        {
            Dictionary<string, string> convTable = new Dictionary<string, string>{
                {"a", "а"},{ "c", "с"}, {"e", "е"}, { "k", "к"},{ "o", "о"},
                { "p", "р"},{ "y", "у"},{ "x", "х"}
            };
            return (convTable.ContainsKey(symbol)) ? convTable[symbol] : symbol;
        }
    }
}

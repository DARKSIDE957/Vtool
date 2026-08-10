using System.Collections.Generic;
using System.Text;

namespace XVR.Tools
{
    /// <summary>
    /// Reshapes Arabic for Unity IMGUI, which draws LTR and does not shape RTL scripts.
    /// </summary>
    public static class VtoolArabicImgui
    {
        private struct Forms
        {
            public char Isolated, Final, Initial, Medial;
            public Forms(int isolated, int final, int initial, int medial)
            {
                Isolated = (char)isolated;
                Final = (char)final;
                Initial = (char)initial;
                Medial = (char)medial;
            }
        }

        private static readonly Dictionary<char, Forms> Map = BuildMap();
        private static readonly HashSet<char> Transparent = new HashSet<char>
        {
            '\u064B', '\u064C', '\u064D', '\u064E', '\u064F', '\u0650',
            '\u0651', '\u0652', '\u0653', '\u0654', '\u0655', '\u0670'
        };

        public static string Fix(string input)
        {
            if (string.IsNullOrEmpty(input) || !ContainsArabic(input))
                return input;

            var runs = new List<string>();
            var buf = new StringBuilder();
            bool arabicRun = IsArabicChar(input[0]) || Transparent.Contains(input[0]);

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                bool isAr = IsArabicChar(c) || Transparent.Contains(c);
                // Digits and Latin stay in LTR runs; spaces stay with current run
                if (c == ' ' || c == '\n' || c == '\t')
                {
                    buf.Append(c);
                    continue;
                }

                if (isAr != arabicRun && buf.Length > 0)
                {
                    runs.Add(ProcessRun(buf.ToString(), arabicRun));
                    buf.Length = 0;
                    arabicRun = isAr;
                }
                else if (buf.Length == 0)
                {
                    arabicRun = isAr;
                }

                buf.Append(c);
            }

            if (buf.Length > 0)
                runs.Add(ProcessRun(buf.ToString(), arabicRun));

            // Visual order for IMGUI: reverse run list so Arabic appears on the right of Latin when mixed
            var result = new StringBuilder();
            for (int i = runs.Count - 1; i >= 0; i--)
                result.Append(runs[i]);
            return result.ToString();
        }

        private static string ProcessRun(string run, bool arabic)
        {
            if (!arabic)
                return run;

            var shaped = new StringBuilder(run.Length);
            for (int i = 0; i < run.Length; i++)
            {
                char c = run[i];
                if (Transparent.Contains(c))
                {
                    shaped.Append(c);
                    continue;
                }

                char baseChar = Normalize(c);

                // Lam + Alef ligatures
                if (baseChar == 'ل' && i + 1 < run.Length)
                {
                    int next = i + 1;
                    while (next < run.Length && Transparent.Contains(run[next])) next++;
                    if (next < run.Length)
                    {
                        char alef = Normalize(run[next]);
                        if (alef == 'ا')
                        {
                            bool prevLink = PrevLinks(run, i);
                            char lig = prevLink ? (char)0xFEFC : (char)0xFEFB; // لا
                            // Preserve original alef variants roughly
                            char orig = run[next];
                            if (orig == 'أ' || orig == 'إ' || orig == 'آ')
                                lig = prevLink ? (char)0xFEF8 : (char)0xFEF7; // لأ approx for أ
                            shaped.Append(lig);
                            // copy tashkeel between lam and alef
                            for (int t = i + 1; t < next; t++)
                                shaped.Append(run[t]);
                            i = next;
                            continue;
                        }
                    }
                }

                if (!Map.ContainsKey(baseChar))
                {
                    shaped.Append(c);
                    continue;
                }

                bool prevConnects = PrevLinks(run, i);
                bool nextConnects = NextLinks(run, i) && LinksToNext(baseChar);

                char glyph;
                if (prevConnects && nextConnects) glyph = Map[baseChar].Medial;
                else if (prevConnects) glyph = Map[baseChar].Final;
                else if (nextConnects) glyph = Map[baseChar].Initial;
                else glyph = Map[baseChar].Isolated;

                shaped.Append(glyph);
            }

            // Reverse Arabic run for LTR rasterization
            var chars = shaped.ToString().ToCharArray();
            System.Array.Reverse(chars);
            return new string(chars);
        }

        private static bool PrevLinks(string s, int i)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                char c = s[j];
                if (Transparent.Contains(c)) continue;
                char n = Normalize(c);
                return Map.ContainsKey(n) && LinksToNext(n);
            }
            return false;
        }

        private static bool NextLinks(string s, int i)
        {
            for (int j = i + 1; j < s.Length; j++)
            {
                char c = s[j];
                if (Transparent.Contains(c)) continue;
                return Map.ContainsKey(Normalize(c));
            }
            return false;
        }

        private static bool LinksToNext(char c)
        {
            // Letters that do not connect to the following letter
            switch (c)
            {
                case 'ا': case 'أ': case 'إ': case 'آ': case 'ٱ':
                case 'د': case 'ذ': case 'ر': case 'ز': case 'و':
                case 'ؤ': case 'ة': case 'ى': case 'ء':
                    return false;
                default:
                    return Map.ContainsKey(c);
            }
        }

        private static char Normalize(char c)
        {
            switch (c)
            {
                case 'أ': case 'إ': case 'آ': case 'ٱ': return 'ا';
                case 'ؤ': return 'و';
                case 'ئ': return 'ي';
                default: return c;
            }
        }

        private static bool ContainsArabic(string s)
        {
            foreach (char c in s)
                if (IsArabicChar(c)) return true;
            return false;
        }

        private static bool IsArabicChar(char c) =>
            (c >= '\u0600' && c <= '\u06FF') ||
            (c >= '\u0750' && c <= '\u077F') ||
            (c >= '\uFB50' && c <= '\uFDFF') ||
            (c >= '\uFE70' && c <= '\uFEFF');

        private static Dictionary<char, Forms> BuildMap()
        {
            // isolated, final, initial, medial (Arabic Presentation Forms-B)
            var m = new Dictionary<char, Forms>
            {
                { 'ء', new Forms(0xFE80, 0xFE80, 0xFE80, 0xFE80) },
                { 'ا', new Forms(0xFE8D, 0xFE8E, 0xFE8D, 0xFE8E) },
                { 'ب', new Forms(0xFE8F, 0xFE90, 0xFE91, 0xFE92) },
                { 'ة', new Forms(0xFE93, 0xFE94, 0xFE93, 0xFE94) },
                { 'ت', new Forms(0xFE95, 0xFE96, 0xFE97, 0xFE98) },
                { 'ث', new Forms(0xFE99, 0xFE9A, 0xFE9B, 0xFE9C) },
                { 'ج', new Forms(0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0) },
                { 'ح', new Forms(0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4) },
                { 'خ', new Forms(0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8) },
                { 'د', new Forms(0xFEA9, 0xFEAA, 0xFEA9, 0xFEAA) },
                { 'ذ', new Forms(0xFEAB, 0xFEAC, 0xFEAB, 0xFEAC) },
                { 'ر', new Forms(0xFEAD, 0xFEAE, 0xFEAD, 0xFEAE) },
                { 'ز', new Forms(0xFEAF, 0xFEB0, 0xFEAF, 0xFEB0) },
                { 'س', new Forms(0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4) },
                { 'ش', new Forms(0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8) },
                { 'ص', new Forms(0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC) },
                { 'ض', new Forms(0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0) },
                { 'ط', new Forms(0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4) },
                { 'ظ', new Forms(0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8) },
                { 'ع', new Forms(0xFEC9, 0xFECA, 0xFECB, 0xFECC) },
                { 'غ', new Forms(0xFECD, 0xFECE, 0xFECF, 0xFED0) },
                { 'ف', new Forms(0xFED1, 0xFED2, 0xFED3, 0xFED4) },
                { 'ق', new Forms(0xFED5, 0xFED6, 0xFED7, 0xFED8) },
                { 'ك', new Forms(0xFED9, 0xFEDA, 0xFEDB, 0xFEDC) },
                { 'ل', new Forms(0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0) },
                { 'م', new Forms(0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4) },
                { 'ن', new Forms(0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8) },
                { 'ه', new Forms(0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC) },
                { 'و', new Forms(0xFEED, 0xFEEE, 0xFEED, 0xFEEE) },
                { 'ى', new Forms(0xFEEF, 0xFEF0, 0xFEEF, 0xFEF0) },
                { 'ي', new Forms(0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4) },
                { 'گ', new Forms(0xFB92, 0xFB93, 0xFB94, 0xFB95) },
                { 'ک', new Forms(0xFB8E, 0xFB8F, 0xFB90, 0xFB91) },
                { 'پ', new Forms(0xFB56, 0xFB57, 0xFB58, 0xFB59) },
                { 'چ', new Forms(0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D) },
                { 'ژ', new Forms(0xFB8A, 0xFB8B, 0xFB8A, 0xFB8B) },
                { 'ڤ', new Forms(0xFB6A, 0xFB6B, 0xFB6C, 0xFB6D) },
            };
            return m;
        }
    }
}

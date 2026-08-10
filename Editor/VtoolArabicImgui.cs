using System.Collections.Generic;
using System.Text;

namespace XVR.Tools
{
    // Shapes + reorders Arabic for Unity IMGUI (LTR, no OpenType shaping).
    public static class VtoolArabicImgui
    {
        private struct Forms
        {
            public readonly char Isolated, Ending, Beginning, Middle;
            public Forms(int isolated, int ending, int beginning, int middle)
            {
                Isolated = (char)isolated;
                Ending = (char)ending;
                Beginning = (char)beginning;
                Middle = (char)middle;
            }
        }

        private static readonly Dictionary<char, Forms> Map = BuildMap();
        private static readonly HashSet<char> Tashkeel = new HashSet<char>
        {
            '\u064B', '\u064C', '\u064D', '\u064E', '\u064F', '\u0650',
            '\u0651', '\u0652', '\u0653', '\u0654', '\u0655', '\u0670',
            '\u0656', '\u0657', '\u0658'
        };

        public static string Fix(string input)
        {
            if (string.IsNullOrEmpty(input) || !ContainsArabic(input))
                return input;

            // Already shaped presentation forms — avoid double-processing
            if (IsMostlyPresentationForms(input))
                return input;

            var lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var sb = new StringBuilder(input.Length + 8);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(FixLine(lines[i]));
            }
            return sb.ToString();
        }

        private static string FixLine(string line)
        {
            if (string.IsNullOrEmpty(line) || !ContainsArabic(line))
                return line;

            // 1) Shape Arabic letters to presentation forms (logical order)
            string shaped = Shape(line);

            // 2) Reverse for LTR IMGUI, but keep Latin / digit / punctuation runs readable
            return ReversePreservingLatin(shaped);
        }

        private static string Shape(string source)
        {
            var chars = source.ToCharArray();
            var output = new List<char>(chars.Length);

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (Tashkeel.Contains(c))
                {
                    output.Add(c);
                    continue;
                }

                if (!IsArabicLetter(c))
                {
                    output.Add(c);
                    continue;
                }

                // Lam + Alef ligatures
                if (IsLam(c))
                {
                    int n = NextLetterIndex(chars, i + 1);
                    if (n >= 0 && IsAlef(chars[n]))
                    {
                        bool prevConnects = PreviousConnects(chars, i);
                        char lig = LamAlefLigature(chars[n], prevConnects);
                        output.Add(lig);
                        // keep tashkeel between lam and alef
                        for (int t = i + 1; t < n; t++)
                            output.Add(chars[t]);
                        i = n;
                        continue;
                    }
                }

                char key = ToMapKey(c);
                if (!Map.TryGetValue(key, out var forms))
                {
                    output.Add(c);
                    continue;
                }

                bool prev = PreviousConnects(chars, i);
                bool next = NextConnects(chars, i) && CanConnectToNext(key);

                char glyph;
                if (prev && next) glyph = forms.Middle;
                else if (prev) glyph = forms.Ending;
                else if (next) glyph = forms.Beginning;
                else glyph = forms.Isolated;

                output.Add(glyph);
            }

            return new string(output.ToArray());
        }

        private static string ReversePreservingLatin(string shaped)
        {
            // Split into Arabic chunks vs non-Arabic chunks, reverse chunk order,
            // reverse only Arabic chunks' characters.
            var chunks = new List<(bool arabic, string text)>();
            var buf = new StringBuilder();
            bool? arabic = null;

            void Flush()
            {
                if (buf.Length == 0 || arabic == null) return;
                chunks.Add((arabic.Value, buf.ToString()));
                buf.Length = 0;
            }

            for (int i = 0; i < shaped.Length; i++)
            {
                char c = shaped[i];
                bool isAr = IsArabicChar(c) || Tashkeel.Contains(c);

                // Spaces/newlines follow the surrounding script when possible
                if (c == ' ' || c == '\t')
                {
                    buf.Append(c);
                    continue;
                }

                if (arabic == null)
                {
                    arabic = isAr;
                    buf.Append(c);
                    continue;
                }

                if (isAr != arabic.Value)
                {
                    Flush();
                    arabic = isAr;
                }
                buf.Append(c);
            }
            Flush();

            var result = new StringBuilder(shaped.Length);
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                var (isArabicChunk, text) = chunks[i];
                if (!isArabicChunk)
                {
                    result.Append(text);
                    continue;
                }

                // Reverse Arabic chunk (presentation forms already shaped)
                for (int j = text.Length - 1; j >= 0; j--)
                    result.Append(text[j]);
            }
            return result.ToString();
        }

        private static int NextLetterIndex(char[] chars, int start)
        {
            for (int i = start; i < chars.Length; i++)
            {
                if (Tashkeel.Contains(chars[i])) continue;
                if (IsArabicLetter(chars[i])) return i;
                return -1;
            }
            return -1;
        }

        private static bool PreviousConnects(char[] chars, int index)
        {
            for (int i = index - 1; i >= 0; i--)
            {
                char c = chars[i];
                if (Tashkeel.Contains(c)) continue;
                if (!IsArabicLetter(c)) return false;
                return CanConnectToNext(ToMapKey(c));
            }
            return false;
        }

        private static bool NextConnects(char[] chars, int index)
        {
            for (int i = index + 1; i < chars.Length; i++)
            {
                char c = chars[i];
                if (Tashkeel.Contains(c)) continue;
                return IsArabicLetter(c);
            }
            return false;
        }

        private static bool CanConnectToNext(char mapKey)
        {
            switch (mapKey)
            {
                case 'ا': case 'د': case 'ذ': case 'ر': case 'ز': case 'و':
                case 'ة': case 'ى': case 'ء': case 'ؤ':
                    return false;
                default:
                    return Map.ContainsKey(mapKey);
            }
        }

        private static bool IsLam(char c) => c == 'ل' || c == 'ڵ';
        private static bool IsAlef(char c) =>
            c == 'ا' || c == 'أ' || c == 'إ' || c == 'آ' || c == 'ٱ';

        private static char LamAlefLigature(char alef, bool prevConnects)
        {
            // Presentation Forms-B lam-alef ligatures
            switch (alef)
            {
                case 'آ': return prevConnects ? (char)0xFEF6 : (char)0xFEF5;
                case 'أ': return prevConnects ? (char)0xFEF8 : (char)0xFEF7;
                case 'إ': return prevConnects ? (char)0xFEFA : (char)0xFEF9;
                default:  return prevConnects ? (char)0xFEFC : (char)0xFEFB; // ا
            }
        }

        private static char ToMapKey(char c)
        {
            switch (c)
            {
                case 'أ': case 'إ': case 'آ': case 'ٱ': return 'ا';
                case 'ؤ': return 'و';
                case 'ئ': return 'ي';
                case 'ة': return 'ة';
                default: return c;
            }
        }

        private static bool IsArabicLetter(char c) =>
            (c >= '\u0621' && c <= '\u064A') ||
            (c >= '\u0671' && c <= '\u06D3') ||
            c == 'پ' || c == 'چ' || c == 'ژ' || c == 'گ' || c == 'ک' || c == 'ڤ' || c == 'ی' || c == 'ک';

        private static bool IsArabicChar(char c) =>
            (c >= '\u0600' && c <= '\u06FF') ||
            (c >= '\u0750' && c <= '\u077F') ||
            (c >= '\u08A0' && c <= '\u08FF') ||
            (c >= '\uFB50' && c <= '\uFDFF') ||
            (c >= '\uFE70' && c <= '\uFEFF');

        private static bool ContainsArabic(string s)
        {
            foreach (char c in s)
                if (IsArabicChar(c)) return true;
            return false;
        }

        private static bool IsMostlyPresentationForms(string s)
        {
            int arabic = 0, presentation = 0;
            foreach (char c in s)
            {
                if (c >= '\uFE70' && c <= '\uFEFF') { presentation++; arabic++; }
                else if (c >= '\uFB50' && c <= '\uFDFF') { presentation++; arabic++; }
                else if (c >= '\u0600' && c <= '\u06FF') arabic++;
            }
            return arabic > 0 && presentation * 2 >= arabic;
        }

        private static Dictionary<char, Forms> BuildMap()
        {
            // isolated, final, initial, medial
            return new Dictionary<char, Forms>
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
                { 'ی', new Forms(0xFEEF, 0xFEF0, 0xFEF3, 0xFEF4) },
                { 'گ', new Forms(0xFB92, 0xFB93, 0xFB94, 0xFB95) },
                { 'ک', new Forms(0xFB8E, 0xFB8F, 0xFB90, 0xFB91) },
                { 'پ', new Forms(0xFB56, 0xFB57, 0xFB58, 0xFB59) },
                { 'چ', new Forms(0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D) },
                { 'ژ', new Forms(0xFB8A, 0xFB8B, 0xFB8A, 0xFB8B) },
                { 'ڤ', new Forms(0xFB6A, 0xFB6B, 0xFB6C, 0xFB6D) },
            };
        }
    }
}

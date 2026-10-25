namespace ProjectPenguin.Presentation.UI
{
    /// <summary>
    /// アプリに同梱している外部の素材のライセンス表示 (メニューの「ライセンス」で出す)。
    /// 一覧の正本はリポジトリの THIRD_PARTY_NOTICES.md。素材を足したら両方に書く。
    /// </summary>
    public static class ThirdPartyNotices
    {
        public sealed class Entry
        {
            public string Name { get; }
            public string License { get; }
            public string Text { get; }

            public Entry(string name, string license, string text)
            {
                Name = name;
                License = license;
                Text = text;
            }
        }

        private const string MitBody =
            "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

        public static readonly Entry[] Entries =
        {
            new Entry("Phosphor Icons", "MIT License", "Copyright (c) 2023 Phosphor Icons\n\n" + MitBody),
            new Entry("M PLUS Rounded 1c", "SIL Open Font License 1.1", "Copyright 2016 The Rounded M+ Project Authors.\nThis Font Software is licensed under the SIL Open Font License, Version 1.1 (https://openfontlicense.org)."),
            new Entry("Oxanium", "SIL Open Font License 1.1", "Copyright 2019 The Oxanium Project Authors (https://github.com/sevmeyer/oxanium).\nThis Font Software is licensed under the SIL Open Font License, Version 1.1 (https://openfontlicense.org)."),
            new Entry("SQLite-net for Unity", "MIT License", "SQLite-net, SQLite3 Multiple Ciphers: MIT License. SQLite: public domain."),
        };
    }
}

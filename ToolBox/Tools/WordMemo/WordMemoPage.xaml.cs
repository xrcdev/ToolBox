using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using Brush = System.Windows.Media.Brush;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using UserControl = System.Windows.Controls.UserControl;

namespace ToolBox.Tools.WordMemo
{
    /// <summary>
    /// 背单词页，移植自 WordMemo.MainWindow（.NET FW 4.6.2 老项目）：
    /// 「单词本」负责增删改查、搜索过滤与 CSV 导入导出；
    /// 「复习」按熟练度从低到高抽词，卡片式自评（认识/模糊/忘记）。
    /// 数据沿用 %LocalAppData%\WordMemo\words.xml，每次变更即时写盘，与老应用互通。
    /// </summary>
    public partial class WordMemoPage : UserControl, IToolPage
    {
        private const int QuizBatchSize = 20;

        private readonly ObservableCollection<WordEntry> _words;
        private bool _suppressEditorSync;

        // 复习状态
        private List<WordEntry>? _quizQueue;
        private int _quizIndex;
        private int _knowCount;
        private int _fuzzyCount;
        private int _forgetCount;
        private bool _quizRunning;
        private bool _revealed;

        public WordMemoPage()
        {
            InitializeComponent();

            _words = new ObservableCollection<WordEntry>(WordStore.Load());
            WordsGrid.ItemsSource = _words;
            ApplySearchFilter();
            RefreshCategoryFilter();
            // RefreshStats 放在最后，状态行保留统计信息
            RefreshStats();
        }

        public string Title => "背单词";

        public void OnHostClosing()
        {
            // 外壳关闭前保存，失败不打断关闭流程
            try
            {
                WordStore.Save(_words);
            }
            catch
            {
            }
        }

        private void SetStatus(string message)
        {
            StatsTextBlock.Text = message;
        }

        // ==================== 单词本 ====================

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            var view = CollectionViewSource.GetDefaultView(_words);
            var keyword = SearchBox.Text.Trim();

            if (keyword.Length == 0)
            {
                view.Filter = null;
                return;
            }

            view.Filter = delegate (object o)
            {
                var entry = o as WordEntry;
                if (entry == null)
                {
                    return false;
                }
                return Contains(entry.Word, keyword)
                    || Contains(entry.Meaning, keyword)
                    || Contains(entry.Category, keyword);
            };
        }

        private static bool Contains(string source, string keyword)
        {
            return source != null && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void WordsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEditorSync)
            {
                return;
            }

            var entry = WordsGrid.SelectedItem as WordEntry;
            if (entry == null)
            {
                ClearEditor();
                return;
            }

            WordInput.Text = entry.Word;
            PhoneticsInput.Text = entry.Phonetics;
            MeaningInput.Text = entry.Meaning;
            ExampleInput.Text = entry.Example;
            CategoryInput.Text = entry.Category;
            NoteInput.Text = entry.Note;
        }

        private void ClearEditor()
        {
            WordInput.Text = string.Empty;
            PhoneticsInput.Text = string.Empty;
            MeaningInput.Text = string.Empty;
            ExampleInput.Text = string.Empty;
            CategoryInput.Text = string.Empty;
            NoteInput.Text = string.Empty;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            _suppressEditorSync = true;
            try
            {
                WordsGrid.SelectedItem = null;
            }
            finally
            {
                _suppressEditorSync = false;
            }

            ClearEditor();
            WordInput.Focus();
            SetStatus("请填写单词与释义，然后点「保存修改」加入词库。");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var word = WordInput.Text.Trim();
            if (word.Length == 0)
            {
                MessageBox.Show(Window.GetWindow(this)!, "请先填写单词。", "提示");
                WordInput.Focus();
                return;
            }

            WordEntry? entry = null;
            foreach (var w in _words)
            {
                if (string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase))
                {
                    entry = w;
                    break;
                }
            }

            var isNew = entry == null;
            if (isNew)
            {
                entry = new WordEntry
                {
                    Word = word,
                    AddedDate = DateTime.Now
                };
                _words.Add(entry);
            }

            entry.Phonetics = PhoneticsInput.Text.Trim();
            entry.Meaning = MeaningInput.Text.Trim();
            entry.Example = ExampleInput.Text.Trim();
            entry.Category = CategoryInput.Text.Trim();
            entry.Note = NoteInput.Text.Trim();

            WordStore.Save(_words);
            CollectionViewSource.GetDefaultView(_words).Refresh();
            RefreshStats();
            RefreshCategoryFilter();

            _suppressEditorSync = true;
            try
            {
                WordsGrid.SelectedItem = entry;
            }
            finally
            {
                _suppressEditorSync = false;
            }

            SetStatus(isNew ? "已新增：" + word : "已更新：" + word);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var entry = WordsGrid.SelectedItem as WordEntry;
            if (entry == null)
            {
                SetStatus("请先在列表中选中要删除的单词。");
                return;
            }

            var confirm = MessageBox.Show(Window.GetWindow(this)!, "确定删除「" + entry.Word + "」吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            _words.Remove(entry);
            WordStore.Save(_words);
            ClearEditor();
            RefreshStats();
            RefreshCategoryFilter();
            SetStatus("已删除：" + entry.Word);
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入 CSV",
                Filter = "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                var result = WordStore.ImportCsv(dialog.FileName, _words);
                WordStore.Save(_words);
                CollectionViewSource.GetDefaultView(_words).Refresh();
                RefreshStats();
                RefreshCategoryFilter();
                SetStatus(string.Format("导入完成：新增 {0} 个，更新 {1} 个。", result.Item1, result.Item2));
                MessageBox.Show(Window.GetWindow(this)!,
                    string.Format("导入完成。\r\n新增 {0} 个，更新 {1} 个。", result.Item1, result.Item2), "导入 CSV");
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!, ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_words.Count == 0)
            {
                SetStatus("词库为空，没有可导出的单词。");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "导出 CSV",
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = "words.csv"
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            {
                return;
            }

            try
            {
                WordStore.ExportCsv(dialog.FileName, _words);
                SetStatus(string.Format("已导出 {0} 个单词到：{1}", _words.Count, dialog.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(Window.GetWindow(this)!, "导出失败：" + ex.Message, "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RefreshStats()
        {
            var mastered = 0;
            foreach (var w in _words)
            {
                if (w.Proficiency >= 4)
                {
                    mastered++;
                }
            }

            if (_words.Count == 0)
            {
                SetStatus("单词总数：0　点「新增单词」后填写并保存，或「导入 CSV」开始积累词库。");
                return;
            }

            SetStatus(string.Format("单词总数：{0}　已掌握（★≥4）：{1}", _words.Count, mastered));
        }

        private void RefreshCategoryFilter()
        {
            // 复习页的范围下拉框：全部分类 + 现有分类去重
            var selected = CategoryFilter.SelectedItem as string;
            CategoryFilter.Items.Clear();
            CategoryFilter.Items.Add("全部分类");

            var categories = new List<string>();
            foreach (var w in _words)
            {
                var c = (w.Category ?? string.Empty).Trim();
                if (c.Length > 0 && !categories.Contains(c))
                {
                    categories.Add(c);
                }
            }
            categories.Sort(StringComparer.CurrentCulture);
            foreach (var c in categories)
            {
                CategoryFilter.Items.Add(c);
            }

            var index = categories.IndexOf(selected);
            CategoryFilter.SelectedIndex = index >= 0 ? index + 1 : 0;
        }

        // ==================== 复习 ====================

        private void StartQuizButton_Click(object sender, RoutedEventArgs e)
        {
            IEnumerable<WordEntry> pool = _words;
            var category = CategoryFilter.SelectedItem as string;
            if (!string.IsNullOrEmpty(category) && category != "全部分类")
            {
                var target = category;
                pool = pool.Where(delegate (WordEntry w) { return w.Category == target; });
            }

            _quizQueue = pool
                .OrderBy(w => w.Proficiency)
                .ThenBy(w => w.LastReviewDate ?? DateTime.MaxValue)
                .Take(QuizBatchSize)
                .ToList();

            if (_quizQueue.Count == 0)
            {
                SetStatus("当前范围内没有可复习的单词，请先在「单词本」里添加。");
                return;
            }

            _quizIndex = 0;
            _knowCount = 0;
            _fuzzyCount = 0;
            _forgetCount = 0;
            _quizRunning = true;
            _revealed = false;

            StartQuizButton.IsEnabled = false;
            StopQuizButton.IsEnabled = true;
            QuizProgress.Maximum = _quizQueue.Count;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (_quizQueue is null)
            {
                return;
            }

            var entry = _quizQueue[_quizIndex];
            QuizProgressText.Text = string.Format("第 {0} / {1} 个　·　认识 {2}　模糊 {3}　忘记 {4}",
                _quizIndex + 1, _quizQueue.Count, _knowCount, _fuzzyCount, _forgetCount);
            QuizProgress.Value = _quizIndex;

            QuizWordText.Text = entry.Word;
            QuizPhoneticsText.Text = entry.Phonetics;
            QuizPhoneticsText.Visibility = entry.Phonetics.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            QuizMeaningText.Text = "释义已隐藏，先回忆一下，再点「显示释义」。";
            QuizMeaningText.Foreground = (Brush)FindResource("TextSecondaryBrush");
            QuizExampleText.Text = string.Empty;
            QuizNoteText.Text = string.Empty;

            _revealed = false;
            ShowMeaningButton.IsEnabled = true;
            RecognizeButton.IsEnabled = false;
            FuzzyButton.IsEnabled = false;
            ForgetButton.IsEnabled = false;
        }

        private void ShowMeaningButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_quizRunning || _revealed || _quizQueue is null)
            {
                return;
            }

            var entry = _quizQueue[_quizIndex];
            QuizMeaningText.Text = entry.Meaning.Length > 0 ? entry.Meaning : "（未填写释义）";
            QuizMeaningText.Foreground = (Brush)FindResource("TextPrimaryBrush");
            QuizExampleText.Text = entry.Example.Length > 0 ? "例句：" + entry.Example : string.Empty;
            QuizNoteText.Text = entry.Note.Length > 0 ? "备注：" + entry.Note : string.Empty;

            _revealed = true;
            ShowMeaningButton.IsEnabled = false;
            RecognizeButton.IsEnabled = true;
            FuzzyButton.IsEnabled = true;
            ForgetButton.IsEnabled = true;
        }

        private void RecognizeButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyGrade(QuizGrade.Known);
        }

        private void FuzzyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyGrade(QuizGrade.Fuzzy);
        }

        private void ForgetButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyGrade(QuizGrade.Forgot);
        }

        private enum QuizGrade
        {
            Known,
            Fuzzy,
            Forgot
        }

        private void ApplyGrade(QuizGrade grade)
        {
            if (!_quizRunning || !_revealed || _quizQueue == null || _quizIndex >= _quizQueue.Count)
            {
                return;
            }

            var entry = _quizQueue[_quizIndex];
            entry.ReviewCount++;
            entry.LastReviewDate = DateTime.Now;

            if (grade == QuizGrade.Known)
            {
                entry.Proficiency = Math.Min(5, entry.Proficiency + 1);
                _knowCount++;
            }
            else if (grade == QuizGrade.Fuzzy)
            {
                _fuzzyCount++;
            }
            else
            {
                entry.Proficiency = 0;
                _forgetCount++;
            }

            WordStore.Save(_words);
            CollectionViewSource.GetDefaultView(_words).Refresh();
            RefreshStats();

            _quizIndex++;
            if (_quizIndex < _quizQueue.Count)
            {
                ShowCurrent();
            }
            else
            {
                FinishQuiz(true);
            }
        }

        private void StopQuizButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_quizRunning)
            {
                return;
            }
            FinishQuiz(false);
        }

        private void FinishQuiz(bool completed)
        {
            _quizRunning = false;
            _revealed = false;
            QuizProgress.Value = QuizProgress.Maximum;

            var reviewed = _knowCount + _fuzzyCount + _forgetCount;
            var summary = string.Format("认识 {0} · 模糊 {1} · 忘记 {2}", _knowCount, _fuzzyCount, _forgetCount);

            if (completed)
            {
                QuizWordText.Text = "本轮复习完成 ✔";
                QuizPhoneticsText.Text = string.Empty;
                QuizMeaningText.Text = summary;
                QuizMeaningText.Foreground = (Brush)FindResource("TextPrimaryBrush");
                QuizExampleText.Text = string.Empty;
                QuizNoteText.Text = string.Empty;
                QuizProgressText.Text = string.Format("共复习 {0} 个单词", reviewed);
                SetStatus(string.Format("复习完成：{0} 个（认识 {1}，模糊 {2}，忘记 {3}）。", reviewed, _knowCount, _fuzzyCount, _forgetCount));
            }
            else
            {
                QuizWordText.Text = "复习已结束";
                QuizPhoneticsText.Text = string.Empty;
                QuizMeaningText.Text = string.Format("已复习 {0} 个 · {1}", reviewed, summary);
                QuizMeaningText.Foreground = (Brush)FindResource("TextPrimaryBrush");
                QuizExampleText.Text = string.Empty;
                QuizNoteText.Text = string.Empty;
                QuizProgressText.Text = string.Format("已复习 {0} / {1} 个", reviewed, _quizQueue != null ? _quizQueue.Count : 0);
                SetStatus("已结束本轮复习，成绩已保存。");
            }

            StartQuizButton.IsEnabled = true;
            StopQuizButton.IsEnabled = false;
            ShowMeaningButton.IsEnabled = false;
            RecognizeButton.IsEnabled = false;
            FuzzyButton.IsEnabled = false;
            ForgetButton.IsEnabled = false;
        }
    }
}

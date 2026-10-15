using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MusicLibrary.Controls;
using Xunit;

namespace MusicLibrary.Tests.Controls;

public sealed class OverflowChipPanelTests
{
    // Простой UIElement-«пенёк»: фиксированный DesiredSize.
    private sealed class FixedSizeElement : UIElement
    {
        private readonly Size _size;
        public FixedSizeElement(double width, double height = 24) { _size = new Size(width, height); }
        protected override Size MeasureCore(Size availableSize) => _size;
    }

    private static OverflowChipPanel BuildPanel(params double[] chipWidths)
    {
        var panel = new OverflowChipPanel();
        foreach (var w in chipWidths)
        {
            panel.Children.Add(new FixedSizeElement(w));
        }
        // Последний — «More» пилюля шириной 80.
        panel.Children.Add(new FixedSizeElement(80));
        return panel;
    }

    private static void DoLayout(OverflowChipPanel panel, double availableWidth, double availableHeight = 40)
    {
        panel.Measure(new Size(availableWidth, availableHeight));
        panel.Arrange(new Rect(0, 0, availableWidth, availableHeight));
    }

    private static void RunInSta(Action test)
    {
        Exception? failure = null;
        var staThread = new Thread(() => { try { test(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true };
        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        if (!staThread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA test exceeded 30 seconds.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void All_Chips_Fit_More_Pill_Hidden()
    {
        RunInSta(() =>
        {
            var panel = BuildPanel(60, 60, 60);
            DoLayout(panel, availableWidth: 300);

            Assert.Equal(Visibility.Visible, panel.Children[0].Visibility);
            Assert.Equal(Visibility.Visible, panel.Children[1].Visibility);
            Assert.Equal(Visibility.Visible, panel.Children[2].Visibility);
            Assert.Equal(Visibility.Collapsed, panel.Children[3].Visibility); // more pill
            Assert.Equal(0, panel.HiddenCount);
        });
    }

    [Fact]
    public void Some_Chips_Hidden_More_Pill_Visible_HiddenCount_Correct()
    {
        // Чипы по 60, More по 80. Доступно 140 px.
        // Грузим жадно: chip[0] 60px влезает, chip[1] 60px уже не влезает (60+60=120 <= 140, но читай ниже).
        // На самом деле chip[1]: 60+60=120 <= 140, то есть влезает.
        // Chip[2]: 120+60=180 > 140, не влезает. После жадной загрузки: lastVisibleChip=1, hiddenCount=1.
        // Так как hiddenCount > 0, резервируем место под More: 120+80=200 > 140.
        // Убираем chip[1]: used=60, lastVisibleChip=0, hiddenCount=2. Проверка: 60+80=140 <= 140. Стоп.
        // Результат: видна только chip[0], chip[1,2] скрыта, More видна, hiddenCount=2.
        RunInSta(() =>
        {
            var panel = BuildPanel(60, 60, 60);
            DoLayout(panel, availableWidth: 140);

            Assert.Equal(Visibility.Visible, panel.Children[0].Visibility);
            Assert.Equal(Visibility.Collapsed, panel.Children[1].Visibility);
            Assert.Equal(Visibility.Collapsed, panel.Children[2].Visibility);
            Assert.Equal(Visibility.Visible, panel.Children[3].Visibility); // more pill
            Assert.Equal(2, panel.HiddenCount);
        });
    }

    [Fact]
    public void Zero_Chips_Renders_Empty_With_Hidden_More()
    {
        RunInSta(() =>
        {
            var panel = new OverflowChipPanel();
            // Только More-пилюля, без chip'ов.
            panel.Children.Add(new FixedSizeElement(80));
            DoLayout(panel, availableWidth: 200);

            Assert.Equal(Visibility.Collapsed, panel.Children[0].Visibility);
            Assert.Equal(0, panel.HiddenCount);
        });
    }

    [Fact]
    public void Exact_Fit_All_Chips_Visible()
    {
        RunInSta(() =>
        {
            // Сумма чипов ровно равна доступной ширине, More не нужен.
            var panel = BuildPanel(100, 100);
            DoLayout(panel, availableWidth: 200);

            Assert.Equal(Visibility.Visible, panel.Children[0].Visibility);
            Assert.Equal(Visibility.Visible, panel.Children[1].Visibility);
            Assert.Equal(Visibility.Collapsed, panel.Children[2].Visibility);
            Assert.Equal(0, panel.HiddenCount);
        });
    }
}

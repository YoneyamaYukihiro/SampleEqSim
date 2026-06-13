using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SampleEqSim.Equipment.Views;

/// <summary>
/// ItemsControl (ListBox/ListView) に項目が追加されたとき、自動で最下部 (最新) へスクロールする添付ビヘイビア。
/// 上=古い / 下=新しい のログ表示で常に最新を表示するために使う。
/// </summary>
public static class AutoScroll
{
    public static readonly DependencyProperty ToEndProperty =
        DependencyProperty.RegisterAttached(
            "ToEnd", typeof(bool), typeof(AutoScroll), new PropertyMetadata(false, OnToEndChanged));

    public static bool GetToEnd(DependencyObject o) => (bool)o.GetValue(ToEndProperty);
    public static void SetToEnd(DependencyObject o, bool v) => o.SetValue(ToEndProperty, v);

    private static void OnToEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl ic || e.NewValue is not true) return;
        if (ic.Items is not INotifyCollectionChanged incc) return;

        incc.CollectionChanged += (_, args) =>
        {
            if (args.Action != NotifyCollectionChangedAction.Add || ic.Items.Count == 0) return;
            var last = ic.Items[ic.Items.Count - 1];
            // レイアウト確定後にスクロールするため Background 優先度で遅延実行
            ic.Dispatcher.BeginInvoke(
                new Action(() => (ic as ListBox)?.ScrollIntoView(last)),
                DispatcherPriority.Background);
        };
    }
}

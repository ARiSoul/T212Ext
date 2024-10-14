using MvvmHelpers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Arisoul.T212.Models;

public class MonthlyDividendGroup
    : ObservableRangeCollection<DividendModel>, INotifyPropertyChanged
{
    public string YearMonth { get; set; }

    private decimal _monthTotalPaid;
    public decimal MonthTotalPaid { get => _monthTotalPaid; set => SetProperty(ref _monthTotalPaid, value); }

    private bool _isExpanded;
    
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    private int _dividendsCount;

    public int DividendsCount { get => _dividendsCount; set => SetProperty(ref _dividendsCount, value); }

    public MonthlyDividendGroup(string yearMonth, int count, decimal totalPaid, IEnumerable<DividendModel> dividends)
        : base(dividends)
    {
        YearMonth = yearMonth;
        DividendsCount = count;
        MonthTotalPaid = totalPaid;
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        onChanged?.Invoke();
        OnPropertyChanged(propertyName);
        return true;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        var changed = PropertyChanged;
        if (changed == null)
            return;

        changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

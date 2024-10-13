using MvvmHelpers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Arisoul.T212.Models;

public class MonthlyDividendGroup
    : ObservableRangeCollection<DividendModel>, INotifyPropertyChanged
{
    public string YearMonth { get; set; }

    public decimal MonthTotalPaid => this.Items.Sum(x => x.AmountInEuro!.Value);

    private bool _isExpanded;

    /// <summary>
    /// Gets or sets the IsExpanded value.
    /// </summary>
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    public MonthlyDividendGroup(string yearMonth, IEnumerable<DividendModel> dividends)
        : base(dividends)
    {
        YearMonth = yearMonth;
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

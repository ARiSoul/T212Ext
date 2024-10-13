using System.Globalization;

namespace Arisoul.T212.App.Converters;

public class InvertedNegativePositiveArrowConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal numericValue)
        {
            return numericValue > 0 ? FaSolid.ArrowUp : FaSolid.ArrowDown;
        }

        return FaSolid.ArrowRight; // Fallback to default color
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NXConfigLauncher.Helpers
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isRunning)
            {
                // 실행 중: 초록색, 미실행: 회색
                return isRunning
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
                    : new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray
            }

            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

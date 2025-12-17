using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MauiApp3
{
    public class Current
    {
        public string? CharCode { get; set; }
        public double Value { get; set; }

        public Current(string charCode, double value)
        {
            CharCode = charCode;
            Value = value;
        }
    }

    public class CurrencyRateResponse
    {
        public DateTime Date { get; set; }
        public Dictionary<string, Valute>? Valute { get; set; }
    }

    public class Valute
    {
        public string CharCode { get; set; } = string.Empty;
        public int Nominal { get; set; }
        public double Value { get; set; }
    }

    public class Converter : INotifyPropertyChanged
    {
        private bool _isUpdating = false;
        private DateTime? _date = DateTime.Today;
        public DateTime? Date
        {
            get { return _date; }
            set
            {
                if (_date != value)
                {
                    _date = value;
                    OnPropertyChanged();
                    _ = HandleDateChanged();
                    OnPropertyChanged(nameof(_value));
                    OnPropertyChanged(nameof(_value2));
                }
            }
        }

        private double _value = 1.0;
        public double value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                    {
                        _isUpdating = true;
                        _ = ConvertForward();
                        OnPropertyChanged(nameof(_value2));
                        _isUpdating = false;
                    }
                }
            }
        }
        private double _value2;
        public double value2
        { 
            get => _value2;
            set
            {
                if (_value2 != value)
                {
                    _value2 = value;
                    OnPropertyChanged();
                    if (!_isUpdating)
                    {
                        _isUpdating = true;
                        _ = ConvertBackward();
                        OnPropertyChanged(nameof(_value));
                        _isUpdating = false;
                    }
                }
            }
        }
        private string _fromCurrency = "USD";
        public string FromCurrency
        {
            get => _fromCurrency;
            set
            {
                if (_fromCurrency != value)
                {
                    _fromCurrency = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(_value));
                    OnPropertyChanged(nameof(_value2));
                    Convert();
                }
            }
        }

        private string _toCurrency = "RUB";
        public string ToCurrency
        {
            get => _toCurrency;
            set
            {
                if (_toCurrency != value)
                {
                    _toCurrency = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(_value));
                    OnPropertyChanged(nameof(_value2));
                    _ = Convert();
                }
            }
        }
        public ObservableCollection<string> AvableCur { get; set; } = new ObservableCollection<string>();
        public DateTime? avalibleDate { get; set; } = DateTime.Now;
        public ObservableCollection<Current> avalible { get; set; } = new ObservableCollection<Current>();
        private static readonly HttpClient client = new();

        public Converter()
        {
            _ = HandleDateChanged();
            LoadSavedState();
        }
        private async Task HandleDateChanged()
        {
            await LoadCurrenciesDate(Date);
            await Convert();
        }
        private void SaveState()
        {
            Preferences.Set("Date", Date.Value.ToString("yyyy-MM-dd"));
            Preferences.Set("From", FromCurrency);
            Preferences.Set("To", ToCurrency);
            Preferences.Set("Value", value);
        }

        private void LoadSavedState()
        {
            var dateStr = Preferences.Get("Date", DateTime.Today.ToString("yyyy-MM-dd"));
            if (DateTime.TryParse(dateStr, out var savedDate))
                _date = savedDate;
            else
                _date = DateTime.Today;
            _fromCurrency = Preferences.Get("From", "USD");
            _toCurrency = Preferences.Get("To", "RUB");
            _value = Preferences.Get("Value", 1.0);
        }
        public async Task LoadCurrenciesDate(DateTime? data)
        {
            if (data == null) return;
            var rates = await GetCurrentState(data);
            if (rates != null)
            {
                string FC = _fromCurrency;
                string SC = _toCurrency;
                AvableCur.Clear();
                var sorted = rates
                    .Where(c => !string.IsNullOrEmpty(c.CharCode))
                    .Select(c => c.CharCode!)
                    .OrderBy(c => c)
                    .ToList();
                foreach (var code in sorted)
                {
                    AvableCur.Add(code);
                }
                if (AvableCur.Contains(FC)) { FromCurrency = FC; }
                if (AvableCur.Contains(SC)) { ToCurrency = SC; }
            }
        }
        private async Task Convert()
        {
            if (Date == null) return;

            var rate = await ConvertAsync(Date.Value);
            value2 = rate.HasValue ? Math.Round(value * rate.Value, 4) : 0;
        }

        public async Task<ObservableCollection<Current>?> GetCurrentState(DateTime? date)
        {
            if (date == null) return null;
            var currentTryDate = date.Value.Date;
            while(currentTryDate.Year>1925)
            {
                try
                {

                    string url = currentTryDate.Date >= DateTime.Today
                        ? "https://www.cbr-xml-daily.ru/daily_json.js"
                        : $"https://www.cbr-xml-daily.ru/archive/{currentTryDate:yyyy}/{currentTryDate:MM}/{currentTryDate:dd}/daily_json.js";

                    string json = await client.GetStringAsync(url);

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var response = JsonSerializer.Deserialize<CurrencyRateResponse>(json, options);

                    if (response?.Valute == null) continue;

                    var collection = new ObservableCollection<Current>();
                    foreach (var kvp in response.Valute)
                    {
                        var v = kvp.Value;
                        double ratePerOne = v.Value / v.Nominal;
                        collection.Add(new Current(v.CharCode, ratePerOne));
                    }
                    collection.Add(new Current("RUB", 1.0));
                    avalibleDate = date;
                    avalible = collection;
                    return collection;
                }
                catch (HttpRequestException ex) when ((int)ex.StatusCode == 404)
                {
                    currentTryDate = currentTryDate.AddDays(-1);
                    _date = currentTryDate;
                    OnPropertyChanged(nameof(Date));
                    continue;
                }
                catch
                {
                    break;
                }
            }
            return null;
        }
        private async Task ConvertForward()
        {
            if (Date == null) return;
            var rate = await ConvertAsync(Date.Value);
            if (rate.HasValue)
            {
                value2 = Math.Round(_value * rate.Value, 4);
            }
        }

        private async Task ConvertBackward()
        {
            if (Date == null) return;
            var rate = await ConvertAsync(Date.Value);
            if (rate.HasValue && rate.Value != 0)
            {
                value = Math.Round(_value2 / rate.Value, 4);
            }
        }

        public async Task<double?> ConvertAsync(DateTime date)
        {
            if(_fromCurrency is null || _toCurrency is null)
            {
                return 0;
            }

            if (_fromCurrency == _toCurrency) return 1.0;
            if (avalibleDate == date)
            {
                var rates = avalible;
                var fromRate = rates.FirstOrDefault(c => c.CharCode == _fromCurrency)?.Value;
                var toRate = rates.FirstOrDefault(c => c.CharCode == _toCurrency)?.Value;
                if (fromRate == null || toRate == null) return null;
                return fromRate.Value / toRate.Value;
            }
            else
            {
                var rates = await GetCurrentState(date);
                if (rates == null) return null;
                var fromRate = rates.FirstOrDefault(c => c.CharCode == _fromCurrency)?.Value;
                var toRate = rates.FirstOrDefault(c => c.CharCode == _toCurrency)?.Value;
                if (fromRate == null || toRate == null) return null;
                return fromRate.Value / toRate.Value;
            }
            
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            SaveState();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
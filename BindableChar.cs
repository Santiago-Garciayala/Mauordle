using System.ComponentModel;
using System.Runtime.CompilerServices;

public class BindableChar : INotifyPropertyChanged
{
    private char _value;

    public char Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName]string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public BindableChar(char initialValue)
    {
        _value = initialValue;
    }
}

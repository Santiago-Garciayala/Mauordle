using System.Collections.ObjectModel;
using System.Text.Json;

namespace Mauordle;

public partial class DesktopHistoryPage : ContentPage
{
	private ObservableCollection<Attempt> _history;
    public ObservableCollection<Attempt> History
    {
        get => _history;
        set
        {
            if (_history != value)
            {
                _history = value;
                OnPropertyChanged();
            }
        }
    }
    public DesktopHistoryPage()
	{
		InitializeComponent();

		BindingContext = this;
		this.Loaded += OnHistoryPageLoaded;
    }

    private async void OnHistoryPageLoaded(object sender, EventArgs e)
	{
        History = await LoadHistory();
    }

    public async static Task<ObservableCollection<Attempt>> LoadHistory()
	{
        string path = Path.Combine(FileSystem.Current.AppDataDirectory, MainPage.HISTORY_FILE_NAME);
		if (!File.Exists(path)) { 
			return new ObservableCollection<Attempt>();
		}

		try
		{
			using (StreamReader reader = new StreamReader(path))
			{
				string loaded = await reader.ReadToEndAsync();
				if (loaded == string.Empty)
					return new ObservableCollection<Attempt>();

				ObservableCollection<Attempt> history = JsonSerializer.Deserialize<ObservableCollection<Attempt>>(loaded);
				return history;
			}
		}
		catch (Exception ex) 
		{ 
			await Shell.Current.DisplayAlert("Failed to load results", ex.Message, "ok");
			return new ObservableCollection<Attempt>();
        }
    }
}
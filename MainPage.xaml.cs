using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Mauordle
{
    public partial class MainPage : ContentPage
    {
        public const int WORD_NUMBER = 6;
        public const int WORD_LENGTH = 5;
        private const string URL = "https://raw.githubusercontent.com/DonH-ITS/jsonfiles/main/words.txt";
        private const int WORDS_FILE_LENGTH = 3103;

        private Entry entry;
        private int wordNum;
        private string[] words; //TODO use this
        private bool wordDisplayCreated = false;
        private bool startFullscreen = false; //mainly for debugging purposes as this will always be true on any release version
        private int guess = 0;
        private ObservableCollection<ObservableCollection<BindableChar>> wordsTyped;
        private bool isUpdatingTyped = false;  //used for UpdateWords
        public bool IsUpdatingTyped { 
            get => isUpdatingTyped;
            set
            {
                if (isUpdatingTyped != value)
                {
                    isUpdatingTyped = value;
                    OnPropertyChanged(nameof(IsUpdatingTyped));
                    OnPropertyChanged(nameof(IsNotUpdatingTyped));
                }
            }
        }
        public bool IsNotUpdatingTyped { get => !IsUpdatingTyped; } //used for entry data binding
        private string typed = "";
        public string Typed
        {
            get => typed;
            set
            {
                if (typed != value)
                {
                    typed = value;
                    OnPropertyChanged(nameof(Typed));
                }
            }
        }

        public MainPage()
        {
            InitializeComponent();

            BindingContext = this;
            PopulateWordsCollection();
            
            Random rand = new Random();
            wordNum = rand.Next(WORDS_FILE_LENGTH);
        }

        private void CreateWordDisplay()
        {
            if (!wordDisplayCreated)
            {
                for (int i = 0; i < 6; i++)
                {
                    HorizontalStackLayout hsl = new HorizontalStackLayout
                    {
                        HorizontalOptions = LayoutOptions.Center
                    };
                    for (int j = 0; j < 5; j++)
                    {
                        Label l = new Label
                        {
                            FontSize = 60,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        };

                        //maui flattens 2D ObservableCOllections when binding for some reason so i have to bind it to wordsTyped[i] directly
                        l.BindingContext = wordsTyped[i];
                        l.SetBinding(Label.TextProperty, new Binding($"[{j}].Value"));
                        


                        Border frame = new Border
                        {
                            BackgroundColor = Colors.Green,
                            WidthRequest = this.Height * .12,
                            HeightRequest = this.Height * .12,
                            Content = l

                        };

                        hsl.Add(frame);
                    }
                    mainVStack.Add(hsl);
                }
                entry = new Entry
                {
                    Margin = 20,
                    WidthRequest = this.Width * .15,
                    HorizontalOptions = LayoutOptions.Center
                };
                entry.SetBinding(Entry.TextProperty, new Binding { 
                    Path = "Typed",
                    Mode = BindingMode.TwoWay
                });
                entry.SetBinding(Entry.IsEnabledProperty, new Binding
                {
                    Path = "IsNotUpdatingTyped",
                });
                entry.TextChanged += Entry_TextChanged;

                
                Label test = new Label();
                test.SetBinding(Label.TextProperty, new Binding { Path = "Typed" });
                mainVStack.Add(test);
                

                mainVStack.Add(entry);
                wordDisplayCreated = true;
            }
        }

        private void Entry_TextChanged(object? sender, TextChangedEventArgs e)
        {
            if (IsUpdatingTyped)
            {
                IsUpdatingTyped = false;
                return;
            }

            Entry entry = (Entry)sender;
            string output = Regex.Replace(e.NewTextValue, "[^a-zA-Z]", "");

            Typed = output.ToUpper();

            if (Typed.Length == WORD_LENGTH)
            {
                IsUpdatingTyped = true;

                UpdateWords(Typed, guess);
                entry.Text = String.Empty;
                ++guess;
            }
        }

        private void PopulateWordsCollection()
        {
            wordsTyped = new ObservableCollection<ObservableCollection<BindableChar>>();
            for (int i = 0; i < WORD_NUMBER; i++)
            {
                ObservableCollection<BindableChar> bogos = new ObservableCollection<BindableChar>();
                for (int j = 0; j < WORD_LENGTH; j++) {
                    bogos.Add(new BindableChar(' '));
                }
                wordsTyped.Add(bogos);
            }
        }

        private string[] GetWords()
        {
            //TODO
            return new string[2];
        }

        private void UpdateWords(string str, int index)
        {
            for(int i = 0; i < WORD_LENGTH; ++i){
                wordsTyped[index][i].Value = str[i];
            }
        }

        /*
        protected override void OnAppearing()
        {
            base.OnAppearing();

        }
        */

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            CreateWordDisplay();
            wordsTyped[0][0].Value = 'b';
        }

        //from: https://stackoverflow.com/questions/76881580/net-6-maui-open-application-in-full-screen-mode-windows
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
#if WINDOWS
            if(startFullscreen){
                  var window = App.Current.Windows.FirstOrDefault().Handler.PlatformView as Microsoft.UI.Xaml.Window;
                  IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                  Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                  Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                 appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
                 //(appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter).Maximize();
            // this line can maximize the window
            }
#endif
        }

    }

}

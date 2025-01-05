namespace Mauordle;
using Plugin.Maui.Audio;

public partial class SettingsPage : ContentPage
{
	private IAudioPlayer bonk;
	public SettingsPage()
	{
		InitializeComponent();

		BindingContext = MainPage.Instance;

		volumeSlider.Loaded += initBonk;

#if ANDROID
		settingsLabel.FontSize = 40;
#endif
	}

    private void volumeSlider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
		if (bonk != null)
		{
			bonk.Volume = volumeSlider.Value;
			bonk.Play();
		}
    }

	private async void initBonk(object sender, EventArgs e) {
		if(bonk == null)
			bonk = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("bonk.mp3"));	
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);	

		volumeSlider.WidthRequest = width * .8;
    }
}
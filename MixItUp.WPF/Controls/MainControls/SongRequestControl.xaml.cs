using Mixer.Base.Util;
using MixItUp.Base;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using MixItUp.Desktop.Services;
using MixItUp.WPF.Util;
using mshtml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MixItUp.WPF.Controls.MainControls
{
    /// <summary>
    /// Interaction logic for SongRequestControl.xaml
    /// </summary>
    public partial class SongRequestControl : MainControlBase
    {
        private const string YouTubeVideoHTMLStart = "<html><body>" +
            "<script>var done = false; function onPlayerStateChange(event) { if (event.data == YT.PlayerState.ENDED) { done = true; } }</script>" +
            "<iframe align=\"center\" height=155 width=275 allow=\"autoplay; encrypted-media\" allowFullScreen autoplay " +
            "src=\"https://www.youtube.com/embed/";
        private const string YoutubeVideoHTMLEnd = "?autoplay=1&rel=0&amp;controls=0&amp;showinfo=0&amp;\" /></body></html>";

        private static SemaphoreSlim songListLock = new SemaphoreSlim(1);

        private ObservableCollection<SongRequestItem> requests = new ObservableCollection<SongRequestItem>();

        private CancellationTokenSource backgroundThreadCancellationTokenSource = new CancellationTokenSource();

        private string YoutubeVideoHTML;

        public SongRequestControl()
        {
            InitializeComponent();
        }

        protected override async Task InitializeInternal()
        {
            GlobalEvents.OnSongRequestsChangedOccurred += GlobalEvents_OnSongRequestsChangedOccurred;

            this.SongRequestsQueueListView.ItemsSource = this.requests;

            this.SongServiceTypeComboBox.ItemsSource = EnumHelper.GetEnumNames<SongRequestServiceTypeEnum>(new List<SongRequestServiceTypeEnum>() { SongRequestServiceTypeEnum.Spotify, SongRequestServiceTypeEnum.Youtube });

            this.SongServiceTypeComboBox.SelectedItem = EnumHelper.GetEnumName(ChannelSession.Settings.SongRequestServiceType);
            this.SpotifyAllowExplicitSongToggleButton.IsChecked = ChannelSession.Settings.SpotifyAllowExplicit;

            await this.RefreshRequestsList();

            this.YoutubeVideoHTML = File.ReadAllText("YouTube\\YoutubeSongRequestPage.html");

            await base.InitializeInternal();
        }

        private async void EnableSongRequestToggleButton_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.SongServiceTypeComboBox.SelectedIndex < 0)
            {
                await MessageBoxHelper.ShowMessageDialog("You must select a song service type.");
                this.EnableSongRequestToggleButton.IsChecked = false;
                return;
            }

            SongRequestServiceTypeEnum service = EnumHelper.GetEnumValueFromString<SongRequestServiceTypeEnum>((string)this.SongServiceTypeComboBox.SelectedItem);
            if (service == SongRequestServiceTypeEnum.Youtube)
            {

            }
            else if (service == SongRequestServiceTypeEnum.Spotify)
            {
                if (ChannelSession.Services.Spotify == null)
                {
                    await MessageBoxHelper.ShowMessageDialog("You must connect to your Spotify account in the Services area.");
                    this.EnableSongRequestToggleButton.IsChecked = false;
                    return;
                }
            }

            await this.Window.RunAsyncOperation(async () =>
            {
                ChannelSession.Settings.SongRequestServiceType = service;
                ChannelSession.Settings.SpotifyAllowExplicit = this.SpotifyAllowExplicitSongToggleButton.IsChecked.GetValueOrDefault();

                this.SongServiceTypeComboBox.IsEnabled = this.SpotifyOptionsGrid.IsEnabled = false;
                this.PlayerControlsGrid.IsEnabled = this.CurrentlyPlayingAndSongQueueGrid.IsEnabled = true;

                await ChannelSession.Services.SongRequestService.Initialize(ChannelSession.Settings.SongRequestServiceType);

                await ChannelSession.SaveSettings();

                await this.RefreshRequestsList();
            });
        }

        private void EnableSongRequestToggleButton_Unchecked(object sender, System.Windows.RoutedEventArgs e)
        {
            ChannelSession.Services.SongRequestService.Disable();

            this.SongServiceTypeComboBox.IsEnabled = this.SpotifyOptionsGrid.IsEnabled = true;
            this.PlayerControlsGrid.IsEnabled = this.CurrentlyPlayingAndSongQueueGrid.IsEnabled = false;
        }

        private void SongServiceTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            this.YoutubeOptionsGrid.Visibility = Visibility.Collapsed;
            this.SpotifyOptionsGrid.Visibility = Visibility.Collapsed;
            this.PlayPauseButton.Visibility = Visibility.Collapsed;

            if (this.SongServiceTypeComboBox.SelectedIndex >= 0)
            {
                SongRequestServiceTypeEnum service = EnumHelper.GetEnumValueFromString<SongRequestServiceTypeEnum>((string)this.SongServiceTypeComboBox.SelectedItem);
                if (service == SongRequestServiceTypeEnum.Youtube)
                {
                    this.YoutubeOptionsGrid.Visibility = Visibility.Visible;
                }
                else if (service == SongRequestServiceTypeEnum.Spotify)
                {
                    this.SpotifyOptionsGrid.Visibility = Visibility.Visible;
                    this.PlayPauseButton.Visibility = Visibility.Visible;
                }
            }
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                if (ChannelSession.Services.SongRequestService.GetRequestService() == SongRequestServiceTypeEnum.Spotify && ChannelSession.Services.Spotify != null)
                {
                    DesktopSongRequestService songRequestService = (DesktopSongRequestService)ChannelSession.Services.SongRequestService;
                    SpotifyPlaylist playlist = await songRequestService.GetSpotifySongRequestPlaylist();

                    SpotifyCurrentlyPlaying currentlyPlaying = await ChannelSession.Services.Spotify.GetCurrentlyPlaying();

                    if (currentlyPlaying != null && currentlyPlaying.ID != null && playlist != null && playlist.Uri.Equals(currentlyPlaying.Uri))
                    {
                        if (currentlyPlaying.IsPlaying)
                        {
                            await ChannelSession.Services.Spotify.PauseCurrentlyPlaying();
                        }
                        else
                        {
                            await ChannelSession.Services.Spotify.PlayCurrentlyPlaying();
                        }
                        return;
                    }

                    if (playlist != null && await ChannelSession.Services.Spotify.PlayPlaylist(playlist))
                    {
                        return;
                    }

                    await MessageBoxHelper.ShowMessageDialog("We could not play the Mix It Up playlist in Spotify. Please ensure Spotify is launched and you have played a song to let Spotify know what device you are on.");
                }
            });
        }

        private async void NextSongButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                if (ChannelSession.Services.SongRequestService.GetRequestService() == SongRequestServiceTypeEnum.Spotify && ChannelSession.Services.Spotify != null)
                {
                    await ChannelSession.Services.Spotify.NextCurrentlyPlaying();
                }
                else if (ChannelSession.Services.SongRequestService.GetRequestService() == SongRequestServiceTypeEnum.Youtube)
                {
                    this.SetYoutubeVideoRender("ZgsByqwaC5k");
                }
            });
        }

        private async void ClearQueueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                if (await MessageBoxHelper.ShowConfirmationDialog("Are you sure you want to clear the Song Request queue?"))
                {
                    await ChannelSession.Services.SongRequestService.ClearAllRequests();
                    await this.RefreshRequestsList();
                }
            });
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                Button button = (Button)sender;
                SongRequestItem songRequest = (SongRequestItem)button.DataContext;
                await ChannelSession.Services.SongRequestService.RemoveSongRequest(songRequest);
                await this.RefreshRequestsList();
            });
        }

        private async void GlobalEvents_OnSongRequestsChangedOccurred(object sender, EventArgs e)
        {
            await this.Dispatcher.InvokeAsync(async () =>
            {
                await this.RefreshRequestsList();
            });
        }

        private async Task RefreshRequestsList()
        {
            await SongRequestControl.songListLock.WaitAsync();

            this.requests.Clear();
            foreach (SongRequestItem item in await ChannelSession.Services.SongRequestService.GetAllRequests())
            {
                this.requests.Add(item);
            }

            SongRequestControl.songListLock.Release();
        }

        private void SetYoutubeVideoRender(string videoID)
        {
            this.YoutubeWebBrowser.NavigateToString(this.YoutubeVideoHTML.Replace("<YOUTUBE VIDEO ID>", videoID));

            Task.Run(async () =>
            {
                bool songDone = false;
                while (!songDone)
                {
                    try
                    {
                        await this.Dispatcher.InvokeAsync(() =>
                        {
                            IHTMLDocument2 document = this.YoutubeWebBrowser.Document as IHTMLDocument2;
                            IHTMLWindow2 window = document.parentWindow as IHTMLWindow2;
                            Type vWindowType = window.GetType();
                            object songDoneValue = vWindowType.InvokeMember("songDone", BindingFlags.GetProperty, null, window, new object[] { });
                            if (songDoneValue is bool)
                            {
                                songDone = (bool)songDoneValue;
                            }
                        });
                    }
                    catch (Exception) { }

                    await Task.Delay(1000);
                }
            });
        }
    }
}

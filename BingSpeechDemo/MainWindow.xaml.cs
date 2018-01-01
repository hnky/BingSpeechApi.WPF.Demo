using System;
using System.Linq;
using System.Windows;
using Microsoft.CognitiveServices.SpeechRecognition;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Media;
using System.Runtime.CompilerServices;
using System.Threading;
using BingSpeechDemo.TextToSpeech;

namespace BingSpeechDemo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private MicrophoneRecognitionClient micClient;

        private string DefaultLocale = "en-US";

        private string SubscriptionKey = ConfigurationManager.AppSettings["SpeechKey"];

        private string AuthenticationUri = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";

        private string accessToken;

        public MainWindow()
        {
            InitializeComponent();
            InitTTS();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (null != micClient)
            {
                micClient.Dispose();
            }
            base.OnClosed(e);
        }


        public void InitTTS()
        {
            Authentication auth = new Authentication(SubscriptionKey);
            accessToken = auth.GetAccessToken();
            WriteLine(accessToken);
        }

      
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            CreateMicrophoneRecoClient();
            micClient.StartMicAndRecognition();
        }

        private void Speak_Click(object sender, RoutedEventArgs e)
        {
            string requestUri = "https://speech.platform.bing.com/synthesize";

            var cortana = new Synthesize();

            cortana.OnAudioAvailable += PlayAudio;
            cortana.OnError += ErrorHandler;

            // Reuse Synthesize object to minimize latency
            cortana.Speak(CancellationToken.None, new Synthesize.InputOptions()
            {
                RequestUri = new Uri(requestUri),
                // Text to be spoken.
                Text = _convertedText.Text,
                VoiceType = Gender.Female,
                // Refer to the documentation for complete list of supported locales.
                Locale = "en-US",
                // You can also customize the output voice. Refer to the documentation to view the different
                // voices that the TTS service can output.
                VoiceName = "Microsoft Server Speech Text to Speech Voice (en-US, ZiraRUS)",
                // Service can return audio in different output format.
                OutputFormat = AudioOutputFormat.Riff16Khz16BitMonoPcm,
                AuthorizationToken = "Bearer " + accessToken,
            }).Wait();

        }

        private static void PlayAudio(object sender, GenericEventArgs<Stream> args)
        {
            Console.WriteLine(args.EventData);

            // For SoundPlayer to be able to play the wav file, it has to be encoded in PCM.
            // Use output audio format AudioOutputFormat.Riff16Khz16BitMonoPcm to do that.
            SoundPlayer player = new SoundPlayer(args.EventData);
            player.PlaySync();
            args.EventData.Dispose();
        }

        private static void ErrorHandler(object sender, GenericEventArgs<Exception> e)
        {
            Console.WriteLine("Unable to complete the TTS request: [{0}]", e.ToString());
        }





// Speech to text

        private void CreateMicrophoneRecoClient()
        {
            micClient = SpeechRecognitionServiceFactory.CreateMicrophoneClient(
                SpeechRecognitionMode.LongDictation,
                DefaultLocale,
                SubscriptionKey);
            micClient.AuthenticationUri = AuthenticationUri;
           
            // Event handlers for speech recognition results
            micClient.OnMicrophoneStatus += OnMicrophoneStatus;

            micClient.OnPartialResponseReceived += OnPartialResponseReceivedHandler;

            micClient.OnResponseReceived += OnMicDictationResponseReceivedHandler;

            micClient.OnConversationError += OnConversationErrorHandler;
        }


        /// <summary>
        /// Called when the microphone status has changed.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MicrophoneEventArgs"/> instance containing the event data.</param>
        private void OnMicrophoneStatus(object sender, MicrophoneEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                WriteLine("--- Microphone status change received by OnMicrophoneStatus() ---");
                WriteLine("********* Microphone status: {0} *********", e.Recording);
                if (e.Recording)
                {
                    WriteLine("Please start speaking.");
                }

                WriteLine();
            });
        }

        /// <summary>
        /// Called when a partial response is received.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PartialSpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnPartialResponseReceivedHandler(object sender, PartialSpeechResponseEventArgs e)
        {
            WriteSpeech(e.PartialResult);

            WriteLine("--- Partial result received by OnPartialResponseReceivedHandler() ---");
            WriteLine("{0}", e.PartialResult);
            WriteLine();
        }

        /// <summary>
        /// Called when a final response is received;
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void OnMicDictationResponseReceivedHandler(object sender, SpeechResponseEventArgs e)
        {
            WriteLine("--- OnMicDictationResponseReceivedHandler ---");
            if (e.PhraseResponse.RecognitionStatus == RecognitionStatus.EndOfDictation ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.DictationEndSilenceTimeout ||
                e.PhraseResponse.RecognitionStatus == RecognitionStatus.RecognitionSuccess
                )
            {
                Dispatcher.Invoke(
                    (Action)(() =>
                    {
                        // we got the final result, so it we can end the mic reco.  No need to do this
                        // for dataReco, since we already called endAudio() on it as soon as we were done
                        // sending all the data.
                        micClient.EndMicAndRecognition();
                        _startButton.IsEnabled = true;
                       
                    }));
            }

            if (e.PhraseResponse.Results.Length > 0)
            {
                WriteSpeech(e.PhraseResponse.Results.First().DisplayText);
            }
            WriteResponseResult(e);
        }

        // <summary>
        /// Called when an error is received.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SpeechErrorEventArgs"/> instance containing the event data.</param>
        private void OnConversationErrorHandler(object sender, SpeechErrorEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _startButton.IsEnabled = true;
            });

            WriteLine("--- Error received by OnConversationErrorHandler() ---");
            WriteLine("Error code: {0}", e.SpeechErrorCode.ToString());
            WriteLine("Error text: {0}", e.SpeechErrorText);
            WriteLine();
        }










        /// <summary>
        /// Writes the response result.
        /// </summary>
        /// <param name="e">The <see cref="SpeechResponseEventArgs"/> instance containing the event data.</param>
        private void WriteResponseResult(SpeechResponseEventArgs e)
        {
            if (e.PhraseResponse.Results.Length == 0)
            {
                WriteLine("No phrase response is available.");
            }
            else
            {
                WriteLine("********* Final n-BEST Results *********");
                for (int i = 0; i < e.PhraseResponse.Results.Length; i++)
                {
                    WriteLine(
                        "[{0}] Confidence={1}, Text=\"{2}\"",
                        i,
                        e.PhraseResponse.Results[i].Confidence,
                        e.PhraseResponse.Results[i].DisplayText);
                }

                WriteLine();
            }
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        private void WriteLine()
        {
            WriteLine(string.Empty);
        }

        /// <summary>
        /// Writes the line.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The arguments.</param>
        private void WriteLine(string format, params object[] args)
        {
            
            var formattedStr = string.Format(format, args);
          //  Trace.WriteLine(formattedStr);
            Dispatcher.Invoke(() =>
            {
                _logText.Text += (formattedStr + "\n");
                _logText.ScrollToEnd();
            });
            
        }

        private void WriteSpeech(string text)
        {
            Dispatcher.Invoke(() =>
            {
                _convertedText.Text = text;
            });
        }

    }
}

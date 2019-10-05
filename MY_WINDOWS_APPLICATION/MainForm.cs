using System.Linq;

namespace MY_WINDOWS_APPLICATION
{
	public partial class MainForm : System.Windows.Forms.Form
	{
		public MainForm()
		{
			InitializeComponent();
		}

		private object syncObj = new object();

		private bool IsRecording { get; set; }

		private int CaptureRegionWidth { get; set; }

		private int CaptureRegionHeight { get; set; }

		private System.DateTime RecordingStartTime { get; set; }

		private Accord.Audio.AudioSourceMixer AudioSourceMixer { get; set; }

		private Accord.Video.FFMPEG.VideoFileWriter VideoFileWriter { get; set; }

		//private Accord.Video.ScreenCaptureStream ScreenCaptureStream { get; set; }

		private void Form1_Load(object sender, System.EventArgs e)
		{
			// **************************************************
			IsRecording = false;
			// **************************************************

			// **************************************************
			//System.Drawing.Rectangle captureRegion =
			//	System.Windows.Forms.Screen.PrimaryScreen.Bounds;

			System.Drawing.Rectangle captureRegion =
				System.Windows.Forms.Screen.AllScreens
				.ToList()[0]
				.Bounds;

			//CaptureRegionWidth = captureRegion.Width;
			//CaptureRegionHeight = captureRegion.Height;

			CaptureRegionWidth = (int)(captureRegion.Width * 1.5);
			CaptureRegionHeight = (int)(captureRegion.Height * 1.5);
			// **************************************************

			// https://support.video.ibm.com/hc/en-us/articles/207852117-Internet-connection-and-recommended-encoding-settings

			// **************************************************
			// 24, 30
			int videoFrameRate = 25;

			// 1200 - 4000 KbPS
			//  1200 * 1024
			//int videoBitRate = CaptureRegionWidth * CaptureRegionHeight;
			int videoBitRate = 200 * 1000;

			int videoKeyFrameInterval =
				System.Convert.ToInt32(1000 / (double)videoFrameRate);

			int videoFrameSize =
				CaptureRegionWidth * CaptureRegionHeight;
			// **************************************************

			// **************************************************
			//timerRecording.Interval = 1000 / videoFrameRate;

			timerRecording.Interval = 100;

			//timerRecording.Interval = 
			//	videoKeyFrameInterval / videoFrameRate;
			// **************************************************

			// **************************************************
			int audioSampleRate = 44100;

			// 320 * 1000
			// 320 * 1024
			// 128 * 1024
			int audioBitRate = 128 * 1000;

			int audioFrameSize = 10 * 4096;
			// **************************************************

			// **************************************************
			string pathName =
				$"D:\\_TEMP\\MOVIE_{ System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") }.avi";

			Accord.Math.Rational frameRate =
				new Accord.Math.Rational
				(numerator: 1000, denominator: videoKeyFrameInterval);
			// **************************************************

			// **************************************************
			VideoFileWriter =
				new Accord.Video.FFMPEG.VideoFileWriter
				{
					Width = CaptureRegionWidth,
					Height = CaptureRegionHeight,

					FrameRate = frameRate,
					BitRate = videoBitRate,

					VideoCodec = Accord.Video.FFMPEG.VideoCodec.H264,
				};

			// visually Lossless
			VideoFileWriter.VideoOptions["crf"] = "18";
			VideoFileWriter.VideoOptions["preset"] = "veryfast";
			VideoFileWriter.VideoOptions["tune"] = "zerolatency";
			VideoFileWriter.VideoOptions["x264opts"] = "no-mbtree:sliced-threads:sync-lookahead=0";
			// **************************************************

			// **************************************************
			System.Guid? audioDeviceGuid = null;

			Accord.DirectSound.AudioDeviceCollection audioDeviceCollection =
				new Accord.DirectSound.AudioDeviceCollection(Accord.DirectSound.AudioDeviceCategory.Capture);

			foreach (Accord.DirectSound.AudioDeviceInfo audioDeviceInfo in audioDeviceCollection)
			{
				audioDeviceGuid = audioDeviceInfo.Guid;

				//System.Windows.Forms.MessageBox.Show
				//	($"Guid: { audioDeviceInfo.Guid } - Description: { audioDeviceInfo.Description }");
			}
			// **************************************************

			// **************************************************
			Accord.DirectSound.AudioCaptureDevice audioCaptureDevice = null;

			if (audioDeviceGuid.HasValue)
			{
				audioCaptureDevice =
					new Accord.DirectSound.AudioCaptureDevice(device: audioDeviceGuid.Value)
					{
						SampleRate = audioSampleRate,
						DesiredFrameSize = audioFrameSize,
						Format = Accord.Audio.SampleFormat.Format32BitIeeeFloat,
					};

				audioCaptureDevice.AudioSourceError += AudioCaptureDevice_AudioSourceError;

				audioCaptureDevice.Start();
			}
			// **************************************************

			// **************************************************
			var audioCaptureDevices =
				new System.Collections.Generic.List<Accord.DirectSound.AudioCaptureDevice>();

			if (audioCaptureDevice != null)
			{
				audioCaptureDevices.Add(audioCaptureDevice);
			}
			// **************************************************

			// **************************************************
			if (audioCaptureDevices.Count > 0)
			{
				AudioSourceMixer =
					new Accord.Audio.AudioSourceMixer(sources: audioCaptureDevices);

				AudioSourceMixer.NewFrame += AudioSourceMixer_NewFrame;
				AudioSourceMixer.AudioSourceError += AudioSourceMixer_AudioSourceError;

				AudioSourceMixer.Start();

				VideoFileWriter.FrameSize = audioFrameSize;
				VideoFileWriter.AudioBitRate = audioBitRate;
				VideoFileWriter.SampleRate = audioSampleRate;
				VideoFileWriter.AudioCodec = Accord.Video.FFMPEG.AudioCodec.Aac;

				VideoFileWriter.AudioLayout =
					AudioSourceMixer.NumberOfChannels == 1 ?
					Accord.Video.FFMPEG.AudioLayout.Mono :
					Accord.Video.FFMPEG.AudioLayout.Stereo;
			}
			// **************************************************

			VideoFileWriter.Open(fileName: pathName);
		}

		private void ScreenCaptureStream_VideoSourceError
			(object sender, Accord.Video.VideoSourceErrorEventArgs eventArgs)
		{
			lock (syncObj)
			{
				if (IsRecording)
				{
					string message =
						$"{ System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") } - { eventArgs.Description } - { eventArgs.Exception.Message }";

					logsListBox.Items.Insert(0, message);
				}
			}
		}

		private void AudioSourceMixer_NewFrame(object sender, Accord.Audio.NewFrameEventArgs e)
		{
			lock (syncObj)
			{
				if (IsRecording)
				{
					VideoFileWriter.WriteAudioFrame(e.Signal);
				}
			}
		}

		private void AudioSourceMixer_AudioSourceError(object sender, Accord.Audio.AudioSourceErrorEventArgs e)
		{
			lock (syncObj)
			{
				if (IsRecording)
				{
					string message =
						$"{ System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") } - { e.Description } - { e.Exception.Message }";

					logsListBox.Items.Insert(0, message);
				}
			}
		}

		private void AudioCaptureDevice_AudioSourceError(object sender, Accord.Audio.AudioSourceErrorEventArgs e)
		{
			lock (syncObj)
			{
				if (IsRecording)
				{
					string message =
						$"{ System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") } - { e.Description } - { e.Exception.Message }";

					logsListBox.Items.Insert(0, message);
				}
			}
		}

		private void TimerRecording_Tick(object sender, System.EventArgs e)
		{
			lock (syncObj)
			{
				if (IsRecording)
				{
					// **************************************************
					//System.DateTime now = System.DateTime.Now;
					//System.DateTime currentFrameTime = now;

					//if (RecordingStartTime == System.DateTime.MinValue)
					//{
					//	RecordingStartTime = now;
					//}

					//System.TimeSpan timestamp =
					//	currentFrameTime - RecordingStartTime;

					//if (timestamp > System.TimeSpan.Zero)
					//{
					//	System.Drawing.Rectangle rectangle =
					//		new System.Drawing.Rectangle(x: 0, y: 0, width: CaptureRegionWidth, height: CaptureRegionHeight);

					//	System.Drawing.Region region = new System.Drawing.Region(rect: rectangle);

					//	System.Drawing.Bitmap bitmap =
					//		new System.Drawing.Bitmap(width: CaptureRegionWidth, height: CaptureRegionHeight);

					//	System.Drawing.Graphics graphics =
					//		System.Drawing.Graphics.FromImage(bitmap);

					//	graphics.CopyFromScreen
					//		(sourceX: 0, sourceY: 0,
					//		destinationX: 0, destinationY: 0,
					//		blockRegionSize: new System.Drawing.Size(width: CaptureRegionWidth, height: CaptureRegionHeight));

					//	VideoFileWriter.WriteVideoFrame
					//		(frame: bitmap, timestamp: timestamp);

					//	//VideoFileWriter.WriteVideoFrame
					//	//	(frame: bitmap, timestamp: timestamp, region: rectangle);
					//}
					// **************************************************

					// **************************************************
					//var bp = new System.Drawing.Bitmap(800, 600);
					//var gr = System.Drawing.Graphics.FromImage(bp);

					//gr.CopyFromScreen
					//	(0, 0, 0, 0, new System.Drawing.Size(bp.Width, bp.Height));

					//VideoFileWriter.WriteVideoFrame(frame: bp);
					// **************************************************

					// **************************************************
					System.Drawing.Bitmap bitmap =
						new System.Drawing.Bitmap
						(width: CaptureRegionWidth, height: CaptureRegionHeight);

					System.Drawing.Graphics graphics =
						System.Drawing.Graphics.FromImage(bitmap);

					graphics.CopyFromScreen
						(sourceX: 0, sourceY: 0,
						destinationX: 0, destinationY: 0,
						blockRegionSize: new System.Drawing.Size(width: CaptureRegionWidth, height: CaptureRegionHeight));

					graphics.Dispose();
					graphics = null;

					VideoFileWriter.WriteVideoFrame(frame: bitmap);

					bitmap.Dispose();
					bitmap = null;
					// **************************************************
				}
			}
		}

		private void StartButton_Click(object sender, System.EventArgs e)
		{
			IsRecording = true;
			RecordingStartTime = System.DateTime.MinValue;

			timerRecording.Start();
		}

		private void StopButton_Click(object sender, System.EventArgs e)
		{
			IsRecording = false;

			timerRecording.Stop();

			VideoFileWriter.Close();
		}
	}
}

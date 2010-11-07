﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HandBrake.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using VidCoder.Properties;
using System.IO;
using Microsoft.Practices.Unity;
using VidCoder.Services;
using System.Threading;
using VidCoder.Model;

namespace VidCoder.ViewModel
{
	public class PreviewViewModel : OkCancelDialogViewModel
	{
		private const int PreviewImageCacheDistance = 3;
		private const string NoSourceTitle = "Preview: No video source";

		private EncodeJob job;
		private HandBrakeInstance previewInstance;
		private ILogger logger = Unity.Container.Resolve<ILogger>();
		private string title;
		private int selectedPreview;
		private bool hasPreview;
		private bool generatingPreview;
		private bool encodeCancelled;
		private double previewPercentComplete;
		private int previewSeconds;
		private int previewCount;

		private ImageSource previewImage;
		private ImageSource[] previewImageCache;
		private Queue<PreviewImageJob> previewImageWorkQueue = new Queue<PreviewImageJob>();
		private bool previewImageQueueProcessing;
		private int updateVersion;
		private object imageSync = new object();

		private ICommand generatePreviewCommand;
		private ICommand cancelPreviewCommand;

		private MainViewModel mainViewModel = Unity.Container.Resolve<MainViewModel>();

		public PreviewViewModel()
		{
			this.previewSeconds = Settings.Default.PreviewSeconds;
			this.Title = NoSourceTitle;

			this.RefreshPreviews();
		}

		public MainViewModel MainViewModel
		{
			get
			{
				return this.mainViewModel;
			}
		}

		public string Title
		{
			get
			{
				return this.title;
			}

			set
			{
				this.title = value;
				this.NotifyPropertyChanged("Title");
			}
		}

		public ImageSource PreviewImage
		{
			get
			{
				return this.previewImage;
			}

			set
			{
				this.previewImage = value;
				this.NotifyPropertyChanged("PreviewImage");
			}
		}

		public bool GeneratingPreview
		{
			get
			{
				return this.generatingPreview;
			}

			set
			{
				this.generatingPreview = value;
				this.NotifyPropertyChanged("GeneratingPreview");
				this.NotifyPropertyChanged("SliderEnabled");
			}
		}

		public bool SliderEnabled
		{
			get
			{
				return this.HasPreview && !this.GeneratingPreview;
			}
		}

		public double PreviewPercentComplete
		{
			get
			{
				return this.previewPercentComplete;
			}

			set
			{
				this.previewPercentComplete = value;
				this.NotifyPropertyChanged("PreviewPercentComplete");
			}
		}

		public int PreviewSeconds
		{
			get
			{
				return this.previewSeconds;
			}

			set
			{
				this.previewSeconds = value;
				this.NotifyPropertyChanged("PreviewSeconds");

				Settings.Default.PreviewSeconds = value;
				Settings.Default.Save();
			}
		}

		public bool HasPreview
		{
			get
			{
				return this.hasPreview;
			}

			set
			{
				this.hasPreview = value;
				this.NotifyPropertyChanged("SliderEnabled");
				this.NotifyPropertyChanged("HasPreview");
			}
		}

		public int SelectedPreview
		{
			get
			{
				return this.selectedPreview;
			}

			set
			{
				this.selectedPreview = value;
				this.NotifyPropertyChanged("SelectedPreview");

				lock (this.imageSync)
				{
					this.PreviewImage = this.previewImageCache[value];
					this.ClearOutOfRangeItems();
					this.BeginBackgroundImageLoad();
				}
			}
		}

		/// <summary>
		/// Gets or sets the width of the preview image in pixels.
		/// </summary>
		public double PreviewWidth { get; set; }

		/// <summary>
		/// Gets or sets the height of the preview image in pixels.
		/// </summary>
		public double PreviewHeight { get; set; }

		public int SliderMax
		{
			get
			{
				if (this.previewCount > 0)
				{
					return this.previewCount - 1;
				}

				return Settings.Default.PreviewCount - 1;
			}
		}

		public HandBrakeInstance ScanInstance
		{
			get
			{
				return this.mainViewModel.ScanInstance;
			}
		}

		public ICommand GeneratePreviewCommand
		{
			get
			{
				if (this.generatePreviewCommand == null)
				{
					this.generatePreviewCommand = new RelayCommand(param =>
					{
						this.job = this.mainViewModel.EncodeJob;
						this.logger.Log("## Generating preview clip");
						this.logger.Log("## Scanning title");

						this.previewInstance = new HandBrakeInstance();
						this.previewInstance.Initialize(Settings.Default.LogVerbosity);
						this.previewInstance.ScanCompleted += this.OnPreviewScanCompleted;
						this.previewInstance.StartScan(this.job.SourcePath, Settings.Default.PreviewCount, this.job.Title);

						this.PreviewPercentComplete = 0;
						this.GeneratingPreview = true;
					},
					param =>
					{
						return this.HasPreview;
					});
				}

				return this.generatePreviewCommand;
			}
		}

		public ICommand CancelPreviewCommand
		{
			get
			{
				if (this.cancelPreviewCommand == null)
				{
					this.cancelPreviewCommand = new RelayCommand(param =>
					{
						this.encodeCancelled = true;
						this.previewInstance.StopEncode();
					});
				}

				return this.cancelPreviewCommand;
			}
		}

		public static void FindAndRefreshPreviews()
		{
			PreviewViewModel previewWindow = WindowManager.FindWindow(typeof(PreviewViewModel)) as PreviewViewModel;
			if (previewWindow != null)
			{
				previewWindow.RefreshPreviews();
			}
		}

		public void RefreshPreviews()
		{
			if (!this.mainViewModel.HasVideoSource)
			{
				this.HasPreview = false;
				this.Title = NoSourceTitle;
				return;
			}

			this.job = this.mainViewModel.EncodeJob;

			EncodingProfile profile = this.job.EncodingProfile;
			int width, height;

			int parWidth, parHeight;
			this.ScanInstance.GetSize(this.job, out width, out height, out parWidth, out parHeight);
			this.PreviewHeight = height;
			this.PreviewWidth = width * ((double)parWidth / parHeight);

			// Update the number of previews.
			this.previewCount = this.ScanInstance.PreviewCount;
			if (this.selectedPreview >= this.previewCount)
			{
				this.selectedPreview = this.previewCount - 1;
				this.NotifyPropertyChanged("SelectedPreview");
			}

			this.NotifyPropertyChanged("SliderMax");

			this.HasPreview = true;

			lock (this.imageSync)
			{
				this.previewImageCache = new ImageSource[this.previewCount];
				this.updateVersion++;
				this.previewImageWorkQueue.Clear();
				this.BeginBackgroundImageLoad();
			}

			if (parWidth == parHeight)
			{
				this.Title = "Preview: " + width + "x" + height;
			}
			else
			{
				this.Title = "Preview: Display " + Math.Round(this.PreviewWidth) + "x" + Math.Round(this.PreviewHeight) + " - Storage " + width + "x" + height;
			}
		}

		private void ClearOutOfRangeItems()
		{
			// Remove out of range items from work queue
			var newWorkQueue = new Queue<PreviewImageJob>();
			while (this.previewImageWorkQueue.Count > 0)
			{
				PreviewImageJob job = this.previewImageWorkQueue.Dequeue();
				if (Math.Abs(job.PreviewNumber - this.SelectedPreview) <= PreviewImageCacheDistance)
				{
					newWorkQueue.Enqueue(job);
				}
			}

			// Remove out of range cache entries
			for (int i = 0; i < this.previewCount; i++)
			{
				if (Math.Abs(i - this.SelectedPreview) > PreviewImageCacheDistance)
				{
					this.previewImageCache[i] = null;
				}
			}
		}

		private void BeginBackgroundImageLoad()
		{
			int initialQueueSize = this.previewImageWorkQueue.Count;
			int currentPreview = this.SelectedPreview;

			if (!ImageLoadedOrLoading(currentPreview))
			{
				this.EnqueueWork(currentPreview);
			}

			for (int i = 1; i <= PreviewImageCacheDistance; i++)
			{
				if (currentPreview - i >= 0 && !ImageLoadedOrLoading(currentPreview - i))
				{
					EnqueueWork(currentPreview - i);
				}

				if (currentPreview + i < this.previewCount && !ImageLoadedOrLoading(currentPreview + i))
				{
					EnqueueWork(currentPreview + i);
				}
			}

			// Start a queue processing thread if one is not going already.
			if (!this.previewImageQueueProcessing && this.previewImageWorkQueue.Count > 0)
			{
				ThreadPool.QueueUserWorkItem(this.ProcessPreviewImageWork);
				this.previewImageQueueProcessing = true;
			}
		}

		private bool ImageLoadedOrLoading(int previewNumber)
		{
			if (this.previewImageCache[previewNumber] != null)
			{
				return true;
			}

			if (this.previewImageWorkQueue.Count(j => j.PreviewNumber == previewNumber) > 0)
			{
				return true;
			}

			return false;
		}

		private void EnqueueWork(int previewNumber)
		{
			this.previewImageWorkQueue.Enqueue(
				new PreviewImageJob
				{
					UpdateVersion = this.updateVersion,
					ScanInstance = this.ScanInstance,
					PreviewNumber = previewNumber,
					EncodeJob = this.job
				});
		}

		private void ProcessPreviewImageWork(object state)
		{
			PreviewImageJob imageJob;
			bool workLeft = true;

			while (workLeft)
			{
				lock (this.imageSync)
				{
					imageJob = this.previewImageWorkQueue.Dequeue();
				}

				ImageSource image = imageJob.ScanInstance.GetPreview(imageJob.EncodeJob, imageJob.PreviewNumber);
				lock (this.imageSync)
				{
					if (imageJob.UpdateVersion == this.updateVersion)
					{
						this.previewImageCache[imageJob.PreviewNumber] = image;
						if (this.SelectedPreview == imageJob.PreviewNumber)
						{
							DispatchService.Invoke(() =>
							{
								this.PreviewImage = image;
							});
						}
					}

					if (this.previewImageWorkQueue.Count == 0)
					{
						workLeft = false;
						this.previewImageQueueProcessing = false;
					}
				}
			}
		}

		private void OnPreviewScanCompleted(object sender, EventArgs eventArgs)
		{
			string extension = null;

			if (this.job.EncodingProfile.OutputFormat == OutputFormat.Mkv)
			{
				extension = ".mkv";
			}
			else
			{
				if (this.job.EncodingProfile.PreferredExtension == OutputExtension.M4v)
				{
					extension = ".m4v";
				}
				else
				{
					extension = ".mp4";
				}
			}

			string previewDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"VidCoder");

			if (!Directory.Exists(previewDirectory))
			{
				Directory.CreateDirectory(previewDirectory);
			}

			this.job.OutputPath = Path.Combine(previewDirectory, @"preview" + extension);
			this.previewInstance.EncodeProgress += (o, e) =>
			{
				this.PreviewPercentComplete = e.FractionComplete * 100;
			};
			this.previewInstance.EncodeCompleted += (o, e) =>
			{
				this.GeneratingPreview = false;

				if (this.encodeCancelled)
				{
					this.logger.Log("# Cancelled preview clip generation");
				}
				else
				{
					if (e.Error)
					{
						this.logger.Log("# Preview clip generation failed");
						Utilities.MessageBox.Show("Preview clip generation failed. See the Log window for details.");
					}
					else
					{
						this.logger.Log("# Finished preview clip generation");
						FileService.Instance.LaunchFile(this.job.OutputPath);
					}
				}
			};

			this.logger.Log("## Encoding clip");
			this.logger.Log("##   Path: " + this.job.OutputPath);
			this.logger.Log("##   Title: " + this.job.Title);
			this.logger.Log("##   Preview #: " + this.SelectedPreview);

			this.encodeCancelled = false;
			this.previewInstance.StartEncode(this.job, true, this.SelectedPreview, this.PreviewSeconds);
		}
	}
}

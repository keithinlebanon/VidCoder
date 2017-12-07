﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VidCoder.Model;
using VidCoder.Services;

namespace VidCoder.ViewModel.DataModels
{
	public class PresetFolderViewModel : ReactiveObject
	{
		private readonly PresetsService presetsService;

		public static PresetFolderViewModel FromPresetFolder(PresetFolder folder, PresetsService passedPresetsService)
		{
			return new PresetFolderViewModel(passedPresetsService)
			{
				Name = folder.Name,
				Id = folder.Id,
				ParentId = folder.ParentId,
			};
		}

		public PresetFolderViewModel(PresetsService presetsService)
		{
			this.presetsService = presetsService;

			IObservable<bool> canRemove = this.WhenAnyValue(x => x.SubFolders.Count, x => x.Items.Count, (numSubfolders, numItems) =>
			{
				return numSubfolders == 0 && numItems == 0 && this.Id != 0;
			});

			this.CreateSubfolder = ReactiveCommand.Create();
			this.CreateSubfolder.Subscribe(_ => this.CreateSubfolderImpl());

			this.RenameFolder = ReactiveCommand.Create();
			this.RenameFolder.Subscribe(_ => this.RenameFolderImpl());

			this.RemoveFolder = ReactiveCommand.Create(canRemove);
			this.RemoveFolder.Subscribe(_ => this.RemoveFolderImpl());
		}

		private string name;
		public string Name
		{
			get { return this.name; }
			set { this.RaiseAndSetIfChanged(ref this.name, value); }
		}

		public long Id { get; set; }

		public long ParentId { get; set; }

		public bool IsNotRoot => this.Id != 0;

		/// <summary>
		/// The flat list of both subfolders and items.
		/// </summary>
		/// <remarks>Subfolders are before items, and everything is alphabetical.</remarks>
		public ReactiveList<object> AllItems { get; } = new ReactiveList<object>();

		public ReactiveList<PresetFolderViewModel> SubFolders { get; } = new ReactiveList<PresetFolderViewModel>();

		public ReactiveList<PresetViewModel> Items { get; } = new ReactiveList<PresetViewModel>();

		private bool isExpanded;
		public bool IsExpanded
		{
			get { return this.isExpanded; }
			set { this.RaiseAndSetIfChanged(ref this.isExpanded, value); }
		}

		public void AddSubfolder(PresetFolderViewModel subfolderViewModel)
		{
			this.SubFolders.Add(subfolderViewModel);

			int insertionIndex;

			// Add in the right place.
			for (insertionIndex = 0; insertionIndex <= this.AllItems.Count; insertionIndex++)
			{
				// If at the end, add there.
				if (insertionIndex == this.AllItems.Count)
				{
					break;
				}

				// If we made it to the presets, add there at end of folder list.
				object item = this.AllItems[insertionIndex];
				var preset = item as PresetViewModel;
				if (preset != null)
				{
					break;
				}

				// If the name compares to less than this folder name, add here.
				var folder = (PresetFolderViewModel)item;
				if (string.Compare(subfolderViewModel.Name, folder.Name, StringComparison.CurrentCultureIgnoreCase) < 0)
				{
					break;
				}
			}

			this.AllItems.Insert(insertionIndex, subfolderViewModel);
		}

		public void RemoveSubfolder(PresetFolderViewModel subFolderViewModel)
		{
			this.SubFolders.Remove(subFolderViewModel);
			this.AllItems.Remove(subFolderViewModel);
		}

		public void AddItem(PresetViewModel presetViewModel)
		{
			this.Items.Add(presetViewModel);

			// Add in the right place.
			int insertionIndex;

			for (insertionIndex = 0; insertionIndex <= this.AllItems.Count; insertionIndex++)
			{
				// If at the end, add there.
				if (insertionIndex == this.AllItems.Count)
				{
					break;
				}

				// If we are still on folders, keep going
				object item = this.AllItems[insertionIndex];
				var folder = item as PresetFolderViewModel;
				if (folder != null)
				{
					continue;
				}

				// If the name compares to less than this preset name, add here.
				var preset = (PresetViewModel)item;
				if (string.Compare(presetViewModel.DisplayName, preset.DisplayName, StringComparison.CurrentCultureIgnoreCase) < 0)
				{
					break;
				}
			}

			this.AllItems.Insert(insertionIndex, presetViewModel);
		}

		public void RemoveItem(PresetViewModel presetViewModel)
		{
			this.Items.Remove(presetViewModel);
			this.AllItems.Remove(presetViewModel);
		}

		public ReactiveCommand<object> CreateSubfolder { get; }
		private void CreateSubfolderImpl()
		{
			this.presetsService.CreateSubFolder(this);
		}

		public ReactiveCommand<object> RenameFolder { get; }
		private void RenameFolderImpl()
		{
			this.presetsService.RenameFolder(this);
		}

		public ReactiveCommand<object> RemoveFolder { get; }
		private void RemoveFolderImpl()
		{
			this.presetsService.RemoveFolder(this);
		}
	}
}
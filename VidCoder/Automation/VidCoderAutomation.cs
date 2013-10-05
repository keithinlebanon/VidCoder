﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VidCoder.Automation
{
	using System.ServiceModel;
	using Microsoft.Practices.Unity;
	using VidCoderCLI;
	using ViewModel.Components;

	public class VidCoderAutomation : IVidCoderAutomation
	{
		public void Encode(string source, string destination, string preset)
		{
			var processingVM = Unity.Container.Resolve<ProcessingViewModel>();

			try
			{
				processingVM.Process(source, destination, preset);
			}
			catch (Exception exception)
			{
				throw new FaultException<AutomationError>(new AutomationError { Message = exception.Message });
			}
		}
	}
}
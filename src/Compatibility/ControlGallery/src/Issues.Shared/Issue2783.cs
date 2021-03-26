﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Maui.Controls.CustomAttributes;
using Microsoft.Maui.Controls.Internals;

namespace Microsoft.Maui.Controls.Compatibility.ControlGallery.Issues
{
	[Preserve(AllMembers = true)]
	[Issue(IssueTracker.Github, 2783, "MemoryLeak in FrameRenderer", PlatformAffected.Android, NavigationBehavior.PushModalAsync)]
	public class Issue2783 : ContentPage
	{
		public Issue2783()
		{
			Frame frPatientInfo = new Frame
			{
				BorderColor = Color.Black,
				BackgroundColor = Color.White,
				HasShadow = true,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
				Padding = 5,
				Content = new AbsoluteLayout
				{
					BackgroundColor = Color.Red,
					HeightRequest = 1000,
					WidthRequest = 2000,
				}
			};

			Content = frPatientInfo;

		}
	}
}
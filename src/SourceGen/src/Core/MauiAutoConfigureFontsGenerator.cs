﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Microsoft.Maui.SourceGen
{
	[Generator]
	public class MauiAutoConfigureFontsGenerator : ISourceGenerator
	{
		public void Initialize(GeneratorInitializationContext context)
		{
		}

		public void Execute(GeneratorExecutionContext context)
		{
			var src = new StringBuilder();

			src.Append(@"
namespace Microsoft.Maui.Hosting
{
    [global::System.CodeDom.Compiler.GeneratedCode(""Microsoft.Maui.SourceGen"", ""1.0.0.0"")]
    public static class FontsMauiAppBuilderExtensions_SourceGen
    {
		[global::System.CodeDom.Compiler.GeneratedCode(""Microsoft.Maui.SourceGen"", ""1.0.0.0"")]
        public static MauiAppBuilder AutoConfigureFonts(this MauiAppBuilder builder, Action<IFontCollection>? configureDelegate)
        {
            builder.ConfigureFonts(fonts => {
");
			foreach (var file in context.AdditionalFiles)
			{
				var extension = Path.GetExtension(file.Path);

				if (extension.Equals(".ttf", StringComparison.InvariantCultureIgnoreCase)
					|| extension.Equals(".otf", StringComparison.InvariantCultureIgnoreCase))
				{
					var alias = context.GetMSBuildItemMetadata(file, "FontAlias");
					var srcAlias = string.IsNullOrWhiteSpace(alias) ? "null" : $"\"{alias}\"";

					var filename = Path.GetFileName(file.Path);
					src.AppendLine(@$"                fonts.AddFont(""{filename}"", {srcAlias});");
				}
			}

			src.AppendLine(@"
                configureDelegate?.Invoke();
            });
        }
    }
}
");
			context.AddSource("Maui_Generated_Font_AutoConfigure.cs", src.ToString());
		}
	}
}
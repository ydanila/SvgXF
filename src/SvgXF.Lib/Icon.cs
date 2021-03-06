﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Xml;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;

namespace SvgXF.Lib
{
    public class Icon : Frame
    {
		#region Bindable Properties

		#region ResourceId

		public static readonly BindableProperty ResourceIdProperty = BindableProperty.Create(
            nameof(ResourceId), typeof(string), typeof(Icon), default(string), propertyChanged: RedrawCanvas);
		
	    public string ResourceId
        {
            get => (string)GetValue(ResourceIdProperty);
            set => SetValue(ResourceIdProperty, value);
        }

		#endregion

		#region Private Members

	    private readonly SKCanvasView m_canvasView = new SKCanvasView();

	    private readonly Assembly m_baseAssembly;

	    #endregion

		#endregion

		#region Constructor

		public Icon()
        {
            Padding = new Thickness(0);
            BackgroundColor = Color.Transparent;
            HasShadow = false;
            Content = m_canvasView;
            m_canvasView.PaintSurface += CanvasViewOnPaintSurface;
			//	here we will find original Xamarin assembly which has item declaration
		    m_baseAssembly = FindBaseAssembly();
	    }

	    #endregion

	    private Assembly FindBaseAssembly()
	    {
		    var frames = new StackTrace().GetFrames();

			
		    var assemblies = (from f in frames
				    select f.GetMethod().ReflectedType.Assembly)
			    .Distinct().ToList();

		    var currentAssemblyName = GetType().Assembly.FullName;

			foreach(var assembly in assemblies)
		    {
				//	skip current assembly
			    if (assembly.FullName.Equals(currentAssemblyName))
			    {
					continue;
			    }

				var name = ParseAssemblyFullName(assembly.FullName);
			    //	ignore .net assemblies
			    if(name.Item2.Equals("7cec85d7bea7798e"))
			    {
				    continue;
			    }

			    //	ignore xamarin lib
			    if(name.Item1.Equals("Xamarin.Forms.Xaml"))
			    {
				    continue;
			    }

			    return assembly;
		    }

		    return null;
	    }

	    private (string, string) ParseAssemblyFullName(string assemblyFullName)
	    {
		    var parts = assemblyFullName.Split(',')
			    .Select(x => x.Trim())
			    .ToList();

		    const string versionToken = "PublicKeyToken=";

		    var name = parts[0];
		    var token = (from p in parts where p.StartsWith(versionToken) select p.Substring(versionToken.Length)).FirstOrDefault();

		    return (name, token ?? "null");
	    }
		
		#region Private Methods

		private static void RedrawCanvas(BindableObject bindable, object oldvalue, object newvalue)
        {
            Icon svgIcon = bindable as Icon;
            svgIcon?.m_canvasView.InvalidateSurface();
        }

        private void CanvasViewOnPaintSurface(object sender, SKPaintSurfaceEventArgs args)
        {
            SKCanvas canvas = args.Surface.Canvas;
            canvas.Clear();

	        if (string.IsNullOrEmpty(ResourceId))
	        {
		        return;
	        }

	        //	use entry if not found
	        var assembly = m_baseAssembly ?? Assembly.GetEntryAssembly();
	        var resId = BuildResourceId(assembly.FullName, ResourceId);
			using (var stream = assembly.GetManifestResourceStream(resId))
            {
	            if (stream == null)
	            {
					throw new Exception($"Resource \'{resId}\' not found");
	            }

                var svg = new SkiaSharp.Extended.Svg.SKSvg();
				var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
	            using(var reader = XmlReader.Create(stream, settings))
	            {
		            svg.Load(reader);
	            }

				SKImageInfo info = args.Info;
                canvas.Translate(info.Width / 2f, info.Height / 2f);

                SKRect bounds = svg.ViewBox;
                float xRatio = info.Width / bounds.Width;
                float yRatio = info.Height / bounds.Height;

                float ratio = Math.Min(xRatio, yRatio);

                canvas.Scale(ratio);
                canvas.Translate(-bounds.MidX, -bounds.MidY);

                canvas.DrawPicture(svg.Picture);
            }
        }

	    private string BuildResourceId(string assemblyFullName, string resourceId)
	    {
		    if (!string.IsNullOrEmpty(resourceId))
		    {
			    resourceId = resourceId.Replace('/', '.').Replace('\\', '.');
		    }

		    var parseResult = ParseAssemblyFullName(assemblyFullName);
		    if (string.IsNullOrEmpty(parseResult.Item1))
		    {
			    return resourceId;
		    }

		    return $"{parseResult.Item1}.{resourceId}";
	    }

	    #endregion
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using GlobalStructures;
using static GlobalStructures.GlobalTools;
using DXGI;
using static DXGI.DXGITools;
using Direct2D;
using WinRT;
using System.Runtime.Intrinsics.Arm;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Capture;
using Microsoft.UI.Xaml.Shapes;
//using Windows.Foundation.Metadata;
using Windows.Graphics;
using Microsoft.Foundation;
using WIC;
using static WIC.WICTools;
using System.Reflection;
using System.Diagnostics;
using static WinUI3_Direct2D_Composition.CSprite;
using System.Diagnostics.Metrics;

//using Windows.Foundation.Metadata;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUI3_Direct2D_Composition
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    // C:\Documents and Settings\Christian\.nuget\packages\microsoft.windowsappsdk\1.5.240404000\include
    // Microsoft.UI.Composition.Interop.h
    public sealed partial class MainWindow : Window
    {
        [ComImport, Guid("FAB19398-6D19-4D8A-B752-8F096C396069"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ICompositorInterop
        {
            //HRESULT CreateCompositionSurfaceForHandle(IntPtr swapChain, out ICompositionSurface result);
            //HRESULT CreateCompositionSurfaceForSwapChain(IntPtr swapChain, out ICompositionSurface result);
            [PreserveSig]
            HRESULT CreateGraphicsDevice(IntPtr renderingDevice, out IntPtr result  /*_COM_Outptr_ ICompositionGraphicsDevice ** result*/);
        }

        [ComImport, Guid("2D6355C2-AD57-4EAE-92E4-4C3EFF65D578"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ICompositionDrawingSurfaceInterop
        {
            //HRESULT BeginDraw(ref RECT updateRect, ref Guid iid, out IntPtr updateObject, out POINT updateOffset);
            [PreserveSig]
            // HRESULT BeginDraw(IntPtr updateRect, [MarshalAs(UnmanagedType.LPStruct)] Guid iid, [MarshalAs(UnmanagedType.Interface)] out ID2D1DeviceContext updateObject, out POINT updateOffset);
            HRESULT BeginDraw(IntPtr updateRect, [MarshalAs(UnmanagedType.LPStruct)] Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object updateObject, out POINT updateOffset);
            [PreserveSig]
            HRESULT EndDraw();
            [PreserveSig]
            HRESULT Resize(SIZE sizePixels);
            [PreserveSig]
            HRESULT Scroll(ref RECT scrollRect, ref RECT clipRect, int offsetX, int offsetY);
            [PreserveSig]
            HRESULT ResumeDraw();
            [PreserveSig]
            HRESULT SuspendDraw();
        }

        [DllImport("User32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);


        ID2D1Factory m_pD2DFactory = null;
        ID2D1Factory1 m_pD2DFactory1 = null;
        IWICImagingFactory m_pWICImagingFactory = null;

        IntPtr m_pD3D11DevicePtr = IntPtr.Zero;
        ID3D11DeviceContext m_pD3D11DeviceContext = null; // Released in Clean : not used
        IDXGIDevice1 m_pDXGIDevice = null; // Released in Clean
        ID2D1Device m_pD2DDevice = null;

        ID2D1DeviceContext m_pD2DDeviceContext = null; // Released in Clean
        ID2D1DeviceContext3 m_pD2DDeviceContext3 = null;
        ID2D1Bitmap m_pD2DBitmapOrangeFish = null;
        ID2D1Bitmap m_pD2DBitmapBlueFish = null;
        ID2D1Bitmap m_pD2DBitmapYellowGreenFish = null;
        ID2D1Bitmap m_pD2DBitmapGrayFish = null;

        IntPtr hWndMain;       
        ICompositionDrawingSurfaceInterop m_pSurfaceInterop =null;
        Microsoft.UI.Composition.SpriteVisual m_SpriteVisual = null;
       
        private Random rand = null;
        private Random randColor = null;
        private const int NB_FISHES = 10;
        private List<CSprite> CSprites = new List<CSprite>();
        //private CSprite spriteBlueFish = null;      

        public MainWindow()
        {
            this.InitializeComponent();
            hWndMain = WinRT.Interop.WindowNative.GetWindowHandle(this);
            this.Title = "WinUI 3 - Direct2D - CompositionDrawingSurface";
            Application.Current.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(Microsoft.UI.Colors.LightSteelBlue);
            Application.Current.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(Microsoft.UI.Colors.RoyalBlue);

            this.SizeChanged += MainWindow_SizeChanged;
            this.Closed += MainWindow_Closed;

            m_pWICImagingFactory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(WICTools.CLSID_WICImagingFactory));
            HRESULT hr = CreateD2D1Factory();
            if (hr == HRESULT.S_OK)
            {
                hr = CreateDeviceContext();
                hr = CreateDeviceResources();
            } 

            var compositor = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(this.Content).Compositor;      
            var compositorInterop = compositor.As<ICompositorInterop>();

            int nWidth = this.AppWindow.ClientSize.Width;
            int nHeight = this.AppWindow.ClientSize.Height;           

            IntPtr pD2DDevice = Marshal.GetIUnknownForObject(m_pD2DDevice);
            IntPtr pCompositionGraphicsDevice = IntPtr.Zero;
            hr = compositorInterop.CreateGraphicsDevice(pD2DDevice, out pCompositionGraphicsDevice);
            if (hr == HRESULT.S_OK)
            {
                Microsoft.UI.Composition.CompositionGraphicsDevice cgd = MarshalInterface<Microsoft.UI.Composition.CompositionGraphicsDevice>.FromAbi(pCompositionGraphicsDevice);
                Size size = new Size(nWidth, nHeight);
                //var _virtualSurface = cgd.CreateVirtualDrawingSurface(size, Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
                Microsoft.UI.Composition.CompositionDrawingSurface drawingSurface = cgd.CreateDrawingSurface(size, Microsoft.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, Microsoft.Graphics.DirectX.DirectXAlphaMode.Premultiplied);
                m_pSurfaceInterop = drawingSurface.As<ICompositionDrawingSurfaceInterop>();
                //Microsoft.UI.Composition.ICompositionSurface pSurface = m_pSurfaceInterop as Microsoft.UI.Composition.ICompositionSurface;     
                //IntPtr pSurfacePtr = Marshal.GetIUnknownForObject(pSurface);
                //Microsoft.UI.Composition.CompositionDrawingSurface drawingSurface = MarshalInterface<Microsoft.UI.Composition.CompositionDrawingSurface>.FromAbi(pSurfacePtr);
                ////Microsoft.UI.Composition.CompositionSurfaceBrush surfaceBrush = compositor.CreateSurfaceBrush(drawingSurface);

                Microsoft.UI.Composition.CompositionSurfaceBrush surfaceBrush = compositor.CreateSurfaceBrush(drawingSurface);
                surfaceBrush.Stretch = Microsoft.UI.Composition.CompositionStretch.None;
                surfaceBrush.HorizontalAlignmentRatio = 0;
                surfaceBrush.VerticalAlignmentRatio = 0;
                //surfaceBrush.TransformMatrix = System.Numerics.Matrix3x2.CreateTranslation(20.0f, 20.0f);

                //object pUpdateObject = null;
                //hr = m_pSurfaceInterop.BeginDraw(IntPtr.Zero, typeof(ID2D1DeviceContext).GUID, out pUpdateObject, out POINT pUpdateOffset);               
                //ID2D1DeviceContext pD2DDeviceContext = pUpdateObject as ID2D1DeviceContext;
                //uint nDPI = GetDpiForWindow(hWndMain);               
                //pD2DDeviceContext.SetDpi(nDPI, nDPI);
                //pD2DDeviceContext.SetTransform(Matrix3x2F.Translation(pUpdateOffset.x * 96.0f / nDPI, pUpdateOffset.y * 96.0f / nDPI));

                //D2D1_SIZE_F sizeDC = pD2DDeviceContext.GetSize();
                //pD2DDeviceContext.Clear(new ColorF(ColorF.Enum.Red, 0.0f));
                //ID2D1SolidColorBrush m_pMainBrush = null;
                //hr = pD2DDeviceContext.CreateSolidColorBrush(new ColorF(ColorF.Enum.Blue, 1.0f), null, out m_pMainBrush);
                ////hr = m_pD2DDeviceContext.CreateSolidColorBrush(new ColorF(ColorF.Enum.Blue, 0.5f), null, out m_pMainBrush);
                //D2D1_POINT_2F ptf = new D2D1_POINT_2F(100, 100);
                //D2D1_ELLIPSE ellipse = new D2D1_ELLIPSE();
                //ellipse.point = ptf;
                //ellipse.radiusX = 90.0f;
                //ellipse.radiusY = 90.0f;
                //pD2DDeviceContext.FillEllipse(ref ellipse, m_pMainBrush);
                //if (m_pMainBrush != null)
                //    SafeRelease(ref m_pMainBrush);
                //hr = m_pSurfaceInterop.EndDraw();
                //SafeRelease(ref pUpdateObject); 

                m_SpriteVisual = compositor.CreateSpriteVisual();
                m_SpriteVisual.Brush = surfaceBrush;
                m_SpriteVisual.Size = new System.Numerics.Vector2(nWidth, nHeight);
                m_SpriteVisual.Offset = new System.Numerics.Vector3(0, 0, 0);
                var root = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(this.Content) as Microsoft.UI.Composition.ContainerVisual;
                Microsoft.UI.Composition.VisualCollection vsChildren = root.Children;
                //vsChildren.InsertAtBottom(m_SpriteVisual);
                vsChildren.InsertAtTop(m_SpriteVisual);          

                //drawingSurface.Dispose();
                //cgd.Dispose();
                Marshal.Release(pCompositionGraphicsDevice);
            }
            Marshal.Release(pD2DDevice);           

            if (stopWatch == null)
            {
                stopWatch = new Stopwatch();
                stopWatch.Start();
            }
            else
                stopWatch.Restart();

            rand = new Random();
            randColor = new Random();

            CompositionTarget.Rendering += CompositionTarget_Rendering;
        }

        int nOrangeFishIncrement = 100;
        int nGrayFishIncrement = 50;
        bool bBlueFish = false;
        bool bYellowGreenFish = false;
        bool bGrayFish = false;

        Stopwatch stopWatch = null;
        private void CompositionTarget_Rendering(object sender, object e)
        {
            if (CSprites.Count < NB_FISHES)
            {
                if (m_pD2DBitmapOrangeFish != null)
                {
                    AddFish(m_pD2DBitmapOrangeFish, 3, 3, 8, 2, 0, "OrangeFish");
                }
            }
            if (m_pD2DBitmapBlueFish != null && !bBlueFish)
            {
                AddFish(m_pD2DBitmapBlueFish, 8, 5, 0, 2, 0, "BlueFish");
                bBlueFish = true;
            }
            if (m_pD2DBitmapYellowGreenFish != null && !bYellowGreenFish)
            {
                AddFish(m_pD2DBitmapYellowGreenFish, 5, 7, 31, 2, 0, "YellowGreenFish");
                bYellowGreenFish = true;
            }
            //if (m_pD2DBitmapGrayFish != null && !bGrayFish)
            //{
            //    AddFish(m_pD2DBitmapGrayFish, 5, 2, 0, 4, 2, "GrayFish");
            //    bGrayFish = true;
            //}

            HRESULT hr = Render();
        }

        HRESULT Render()
        {
            HRESULT hr = HRESULT.S_OK;
            if (m_pSurfaceInterop != null)
            {
                object pUpdateObject = null;
                hr = m_pSurfaceInterop.BeginDraw(IntPtr.Zero, typeof(ID2D1DeviceContext).GUID, out pUpdateObject, out POINT pUpdateOffset);
                // When size too small
                // 0x80131509 {"Operation is not valid due to the current state of the object."}
                if (hr == HRESULT.S_OK)
                {
                    ID2D1DeviceContext pD2DDeviceContext = pUpdateObject as ID2D1DeviceContext;
                    ID2D1DeviceContext3 pD2DDeviceContext3 = (ID2D1DeviceContext3)pD2DDeviceContext;
                    uint nDPI = GetDpiForWindow(hWndMain);
                    float nScale = nDPI / 96.0f;
                    // Force 96 DPI otherwise fishes are too big...
                    // pD2DDeviceContext3.SetDpi(nDPI, nDPI);
                    // pD2DDeviceContext3.SetTransform(Matrix3x2F.Translation(pUpdateOffset.x * 96.0f / nDPI, pUpdateOffset.y * 96.0f / nDPI));
                    pD2DDeviceContext3.SetDpi(96.0f, 96.0f);
                    pD2DDeviceContext3.SetTransform(Matrix3x2F.Translation(pUpdateOffset.x, pUpdateOffset.y));
                   
                    // Weird size... (bigger than client area)
                    pD2DDeviceContext.GetSize(out D2D1_SIZE_F size);

                    pD2DDeviceContext3.Clear(new ColorF(ColorF.Enum.Red, 0.0f));

                    pD2DDeviceContext3.GetAntialiasMode(out D2D1_ANTIALIAS_MODE nOldAntialiasMode);
                    pD2DDeviceContext3.SetAntialiasMode(D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);

                    foreach (CSprite s in CSprites)
                    {
                        //RECT rect;
                        //GetClientRect(hWndMain, out rect);
                        float nWidth = (float)this.AppWindow.ClientSize.Width / nScale;
                        float nHeight = (float)this.AppWindow.ClientSize.Height / nScale;                      

                        s.X += ((rand.NextSingle()) * s.StepX);
                        s.Y += ((rand.NextSingle()) * s.StepY);
                        s.Move(new D2D1_SIZE_F(nWidth, nHeight), pD2DDeviceContext3, (s.Tag == "OrangeFish" || s.Tag == "YellowGreenFish" || s.Tag == "GrayFish") ? HORIZONTALFLIP.LEFT : HORIZONTALFLIP.RIGHT, BOUNCE.BOTH);
                        s.Draw(pD2DDeviceContext3, s.CurrentIndex, 1, true);
                        if (s.Tag == "OrangeFish")
                        {
                            if (stopWatch.ElapsedMilliseconds - s.StartTime >= nOrangeFishIncrement)
                            {
                                s.CurrentIndex++;
                                s.StartTime = stopWatch.ElapsedMilliseconds;
                            }
                        }
                        else if (s.Tag == "BlueFish")
                        {
                            s.CurrentIndex++;                           
                        }
                        else if (s.Tag == "YellowGreenFish")
                        {
                            s.CurrentIndex++;
                        }
                        else if (s.Tag == "GrayFish")
                        {
                            if (stopWatch.ElapsedMilliseconds - s.StartTime >= nGrayFishIncrement)
                            {
                                s.CurrentIndex++;
                                s.StartTime = stopWatch.ElapsedMilliseconds;
                            }
                        }
                    }
                    
                    pD2DDeviceContext3.SetAntialiasMode(nOldAntialiasMode);

                    hr = m_pSurfaceInterop.EndDraw();
                    SafeRelease(ref pD2DDeviceContext);
                }
            }
            return hr;
        } 

        private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            HRESULT hr = HRESULT.S_OK;
            if (m_SpriteVisual != null && m_pSurfaceInterop != null)
            {
                // 0x80131509 : 'Operation is not valid due to the current state of the object.'.
                //hr = m_pSurfaceInterop.SuspendDraw();
                //if (hr != HRESULT.S_OK)
                //    Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
                m_SpriteVisual.Size = new System.Numerics.Vector2((float)args.Size.Width, (float)args.Size.Height);
                //hr = m_pSurfaceInterop.ResumeDraw();

                SIZE sz = new SIZE((int)args.Size.Width, (int)args.Size.Height);
                hr = m_pSurfaceInterop.Resize(sz);               
            }
        }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            AddFish(m_pD2DBitmapOrangeFish, 3, 3, 8, 3, 0, "OrangeFish");
        }

        HRESULT CreateD2D1Factory()
        {
            HRESULT hr = HRESULT.S_OK;
            D2D1_FACTORY_OPTIONS options = new D2D1_FACTORY_OPTIONS();

            // Needs "Enable native code Debugging"
#if DEBUG
            options.debugLevel = D2D1_DEBUG_LEVEL.D2D1_DEBUG_LEVEL_INFORMATION;
#endif

            hr = D2DTools.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, ref D2DTools.CLSID_D2D1Factory, ref options, out m_pD2DFactory);
            m_pD2DFactory1 = (ID2D1Factory1)m_pD2DFactory;
            return hr;
        }

        public HRESULT CreateDeviceContext()
        {

            HRESULT hr = HRESULT.S_OK;
            uint creationFlags = (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT;

            // Needs "Enable native code Debugging"
#if DEBUG
            creationFlags |= (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG;
#endif

            int[] aD3D_FEATURE_LEVEL = new int[] { (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
                (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_3,
                (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_2, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1};

            D3D_FEATURE_LEVEL featureLevel;
            hr = D2DTools.D3D11CreateDevice(null,    // specify null to use the default adapter
                D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                creationFlags,              // optionally set debug and Direct2D compatibility flags
                                            //pD3D_FEATURE_LEVEL,              // list of feature levels this app can support
                aD3D_FEATURE_LEVEL,
                //(uint)Marshal.SizeOf(aD3D_FEATURE_LEVEL),   // number of possible feature levels
                (uint)aD3D_FEATURE_LEVEL.Length,
                D2DTools.D3D11_SDK_VERSION,
                out m_pD3D11DevicePtr,                    // returns the Direct3D device created
                out featureLevel,            // returns feature level of device created
                                             //out pD3D11DeviceContextPtr                    // returns the device immediate context
                out m_pD3D11DeviceContext
            );
            if (hr == HRESULT.S_OK)
            {
                //m_pD3D11DeviceContext = Marshal.GetObjectForIUnknown(pD3D11DeviceContextPtr) as ID3D11DeviceContext;             

                //ID2D1Multithread m_D2DMultithread;
                //m_D2DMultithread = (ID2D1Multithread)m_pD2DFactory1;

                //m_pD2DFactory1.GetDesktopDpi(out float x, out float y);

                m_pDXGIDevice = Marshal.GetObjectForIUnknown(m_pD3D11DevicePtr) as IDXGIDevice1;
                if (m_pD2DFactory1 != null)
                {
                    hr = m_pD2DFactory1.CreateDevice(m_pDXGIDevice, out m_pD2DDevice);
                    if (hr == HRESULT.S_OK)
                    {
                        hr = m_pD2DDevice.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE, out m_pD2DDeviceContext);
                        //SafeRelease(ref pD2DDevice);
                    }
                }
                //Marshal.Release(m_pD3D11DevicePtr);
            }
            return hr;
        } 

        HRESULT LoadBitmapFromFile(ID2D1DeviceContext3 pDeviceContext3, IWICImagingFactory pIWICFactory, string uri, uint destinationWidth,
            uint destinationHeight, out ID2D1Bitmap pD2DBitmap, out IWICBitmapSource pBitmapSource)
        {
            HRESULT hr = HRESULT.S_OK;
            pD2DBitmap = null;
            pBitmapSource = null;

            IWICBitmapDecoder pDecoder = null;
            IWICBitmapFrameDecode pSource = null;
            IWICFormatConverter pConverter = null;
            IWICBitmapScaler pScaler = null;

            hr = pIWICFactory.CreateDecoderFromFilename(uri, Guid.Empty, unchecked((int)GENERIC_READ), WICDecodeOptions.WICDecodeMetadataCacheOnLoad, out pDecoder);
            if (hr == HRESULT.S_OK)
            {
                hr = pDecoder.GetFrame(0, out pSource);
                if (hr == HRESULT.S_OK)
                {
                    hr = pIWICFactory.CreateFormatConverter(out pConverter);
                    if (hr == HRESULT.S_OK)
                    {
                        if (destinationWidth != 0 || destinationHeight != 0)
                        {
                            uint originalWidth, originalHeight;
                            hr = pSource.GetSize(out originalWidth, out originalHeight);
                            if (hr == HRESULT.S_OK)
                            {
                                if (destinationWidth == 0)
                                {
                                    float scalar = (float)(destinationHeight) / (float)(originalHeight);
                                    destinationWidth = (uint)(scalar * (float)(originalWidth));
                                }
                                else if (destinationHeight == 0)
                                {
                                    float scalar = (float)(destinationWidth) / (float)(originalWidth);
                                    destinationHeight = (uint)(scalar * (float)(originalHeight));
                                }
                                hr = pIWICFactory.CreateBitmapScaler(out pScaler);
                                if (hr == HRESULT.S_OK)
                                {
                                    hr = pScaler.Initialize(pSource, destinationWidth, destinationHeight, WICBitmapInterpolationMode.WICBitmapInterpolationModeCubic);
                                    if (hr == HRESULT.S_OK)
                                    {
                                        hr = pConverter.Initialize(pScaler, GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0f, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
                                        //hr = pConverter.Initialize(pScaler, GUID_WICPixelFormat32bppBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0f, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
                                    }
                                    Marshal.ReleaseComObject(pScaler);
                                }
                            }
                        }
                        else // Don't scale the image.
                        {
                            hr = pConverter.Initialize(pSource, GUID_WICPixelFormat32bppPBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0f, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
                            //hr = pConverter.Initialize(pSource, GUID_WICPixelFormat32bppBGRA, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0.0f, WICBitmapPaletteType.WICBitmapPaletteTypeMedianCut);
                        }

                        // Create a Direct2D bitmap from the WIC bitmap.
                        D2D1_BITMAP_PROPERTIES bitmapProperties = new D2D1_BITMAP_PROPERTIES();
                        bitmapProperties.pixelFormat = D2DTools.PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED);
                        bitmapProperties.dpiX = 96;
                        bitmapProperties.dpiY = 96;
                        hr = pDeviceContext3.CreateBitmapFromWicBitmap(pConverter, bitmapProperties, out pD2DBitmap);

                        //if (pBitmapSource != null)
                        pBitmapSource = pConverter;
                    }
                    Marshal.ReleaseComObject(pSource);
                }
                Marshal.ReleaseComObject(pDecoder);
            }
            return hr;
        }   

        private void AddFish(ID2D1Bitmap pBitmap, int nXSprite, int nYSprite, int nCountSprite, int nSpeedMax, int nSpeedSup = 0, string sTag = "")
        {
            if (pBitmap != null)
            {
                CSprite s = null;
                D2D1_SIZE_F size = new D2D1_SIZE_F(this.AppWindow.ClientSize.Width, this.AppWindow.ClientSize.Height);
                float nClientWidth = (float)size.width;
                float nClientHeight = (float)size.height;

                float nScale = rand.NextSingle() * 1;
                D2D1_MATRIX_3X2_F scale = new D2D1_MATRIX_3X2_F();
                scale._11 = nScale;
                scale._22 = nScale;
                Array colors = ColorF.Enum.GetValues(typeof(ColorF.Enum));
                ColorF.Enum randomColor;
                randomColor = (ColorF.Enum)colors.GetValue(randColor.Next(colors.Length));
                if (sTag == "OrangeFish")
                    s = new CSprite(m_pD2DDeviceContext3, pBitmap, (uint)nXSprite, (uint)nYSprite, (uint)nCountSprite, rand.NextSingle() * nSpeedMax + nSpeedSup, rand.NextSingle() * nSpeedMax + nSpeedSup, new ColorF(randomColor), scale);
                else if (sTag == "BlueFish")
                    s = new CSprite(m_pD2DDeviceContext3, pBitmap, (uint)nXSprite, (uint)nYSprite, (uint)nCountSprite, rand.NextSingle() * nSpeedMax + nSpeedSup, rand.NextSingle() * nSpeedMax + nSpeedSup, null, null);
                else if (sTag == "YellowGreenFish")
                    s = new CSprite(m_pD2DDeviceContext3, pBitmap, (uint)nXSprite, (uint)nYSprite, (uint)nCountSprite, rand.NextSingle() * nSpeedMax + nSpeedSup, rand.NextSingle() * nSpeedMax + nSpeedSup, null, null);
                else if (sTag == "GrayFish")
                    s = new CSprite(m_pD2DDeviceContext3, pBitmap, (uint)nXSprite, (uint)nYSprite, (uint)nCountSprite, rand.NextSingle() * nSpeedMax + nSpeedSup, rand.NextSingle() * nSpeedMax + nSpeedSup, null, null);
                s.StartTime = stopWatch.ElapsedMilliseconds;
                s.Tag = sTag;
                CSprites.Add(s);
                
                pBitmap.GetSize(out D2D1_SIZE_F bmpSize);
                float nWidth = bmpSize.width / nXSprite;
                float nHeight = bmpSize.width / nYSprite;
                if (scale._11 != 0)
                {
                    //nWidth *= scale._11;
                    nClientWidth *= 1 / scale._11;
                }
                if (scale._22 != 0)
                {
                    //nHeight *= scale._22;
                    nClientHeight *= 1 / scale._22;
                }

                float nX = rand.NextSingle() * nClientWidth;
                float nY = rand.NextSingle() * nClientHeight;
                if (nX + nWidth >= nClientWidth)
                    nX = nClientWidth - nWidth;
                if (nX <= 0)
                    nX = 0;
                if (nY + nHeight >= nClientHeight)
                    nY = nClientHeight - nHeight;
                if (nY <= 0)
                    nY = 0;
                s.X = nX;
                s.Y = nY;
            }
        }

        HRESULT CreateDeviceResources()
        {
            HRESULT hr = HRESULT.S_OK;
            if (m_pD2DDeviceContext != null)
            {
                if (m_pD2DDeviceContext3 == null)
                    m_pD2DDeviceContext3 = (ID2D1DeviceContext3)m_pD2DDeviceContext;

                string sExePath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                IWICBitmapSource pWICBitmapSource = null;
                string sAbsolutePath = sExePath + "/Assets/Orange_Fish.png";
                hr = LoadBitmapFromFile(m_pD2DDeviceContext3, m_pWICImagingFactory, sAbsolutePath, 0, 0, out m_pD2DBitmapOrangeFish, out pWICBitmapSource);
                SafeRelease(ref pWICBitmapSource);

                IWICBitmapSource pWICBitmapSource2 = null;
                sAbsolutePath = sExePath + "/Assets/Blue_Fish.png";
                hr = LoadBitmapFromFile(m_pD2DDeviceContext3, m_pWICImagingFactory, sAbsolutePath, 0, 0, out m_pD2DBitmapBlueFish, out pWICBitmapSource2);
                SafeRelease(ref pWICBitmapSource2);

                IWICBitmapSource pWICBitmapSource3 = null;
                sAbsolutePath = sExePath + "/Assets/YellowGreen_Fish.png";
                hr = LoadBitmapFromFile(m_pD2DDeviceContext3, m_pWICImagingFactory, sAbsolutePath, 0, 0, out m_pD2DBitmapYellowGreenFish, out pWICBitmapSource3);
                SafeRelease(ref pWICBitmapSource3);

                IWICBitmapSource pWICBitmapSource4 = null;
                sAbsolutePath = sExePath + "/Assets/Gray_Fish.png";
                hr = LoadBitmapFromFile(m_pD2DDeviceContext3, m_pWICImagingFactory, sAbsolutePath, 0, 0, out m_pD2DBitmapGrayFish, out pWICBitmapSource4);
                SafeRelease(ref pWICBitmapSource4);
            }
            return hr;
        }

        void CleanDeviceResources()
        {
            SafeRelease(ref m_pD2DBitmapOrangeFish);
            SafeRelease(ref m_pD2DBitmapBlueFish);
            SafeRelease(ref m_pD2DBitmapYellowGreenFish);
            SafeRelease(ref m_pD2DBitmapGrayFish);
        }

        void Clean()
        {
            CleanDeviceResources();
            SafeRelease(ref m_pD2DDeviceContext);
            SafeRelease(ref m_pSurfaceInterop);            
            SafeRelease(ref m_pD2DDevice);

            SafeRelease(ref m_pDXGIDevice);
            SafeRelease(ref m_pD3D11DeviceContext);
            Marshal.Release(m_pD3D11DevicePtr);

            SafeRelease(ref m_pWICImagingFactory);
            SafeRelease(ref m_pD2DFactory1);
            SafeRelease(ref m_pD2DFactory);

            //if (spriteBlueFish != null)
            //    spriteBlueFish.Dispose();
            foreach (CSprite s in CSprites)
                s.Dispose();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Clean();
        }
    }
}

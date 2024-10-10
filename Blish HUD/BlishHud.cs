using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Color = Microsoft.Xna.Framework.Color;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;


namespace Blish_HUD {

    public class BlishHud : Game {

        private static readonly Logger Logger = Logger.GetLogger<BlishHud>();

        #region Internal Members for Services

        /// <summary>
        /// Exposed through the <see cref="GraphicsService"/>'s <see cref="GraphicsService.GraphicsDeviceManager"/>.
        /// </summary>
        internal GraphicsDeviceManager ActiveGraphicsDeviceManager { get; }

        /// <summary>
        /// Exposed through the <see cref="ContentService"/>'s <see cref="ContentService.ContentManager"/>.
        /// </summary>
        internal Microsoft.Xna.Framework.Content.ContentManager ActiveContentManager { get; }

        internal static BlishHud Instance;

        #endregion

        public IntPtr FormHandle { get; private set; }

        public Form Form { get; private set; }

        // TODO: Move this into GraphicsService
        public RasterizerState UiRasterizer { get; private set; }

        // Primarily used to draw debug text
        private SpriteBatch _basicSpriteBatch;

        private RenderTarget2D _renderTexture = null;
        private MemoryMappedFile _mmf = null;
        private Color[] _buffer = null;

        public BlishHud() {
            BlishHud.Instance = this;

            this.ActiveGraphicsDeviceManager = new GraphicsDeviceManager(this);
            this.ActiveGraphicsDeviceManager.PreparingDeviceSettings += delegate (object sender, PreparingDeviceSettingsEventArgs args) {
                args.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 1;

            };

            this.ActiveGraphicsDeviceManager.GraphicsProfile = GraphicsProfile.HiDef;
            this.ActiveGraphicsDeviceManager.PreferMultiSampling = true;

            this.ActiveContentManager = this.Content;

            this.Content.RootDirectory = "Content";

            this.IsMouseVisible = true;
        }

        protected override void Initialize() {
            FormHandle = this.Window.Handle;
            Form = Control.FromHandle(FormHandle).FindForm();

            Form.BackColor = System.Drawing.Color.Black;
            // Avoid the flash the window shows when the application launches (-32000x-32000 is where windows places minimized windows)
            Form.Location = new System.Drawing.Point(-32000, -32000);

            if (!File.Exists("OpacityFix")) {
                // Causes an issue with it showing a black box if we don't set this to true
                this.Window.IsBorderless = true;
            }

            this.Window.AllowAltF4 = false;
            this.InactiveSleepTime = TimeSpan.Zero;

            // Initialize all game services
            foreach (var service in GameService.All) {
                service.DoInitialize(this);
            }

            base.Initialize();
        }

        protected override void LoadContent() {
            UiRasterizer = new RasterizerState() {
                ScissorTestEnable = true
            };

            // Create a new SpriteBatch, which can be used to draw debug information
            _basicSpriteBatch = new SpriteBatch(this.GraphicsDevice);
        }

        protected override void BeginRun() {
            base.BeginRun();

            Logger.Debug("Loading services.");

            // Let all of the game services have a chance to load
            foreach (var service in GameService.All) {
                service.DoLoad();
            }
        }

        protected override void EndRun() {
            _renderTexture?.Dispose();
            
            if (_mmf != null && _buffer != null) {
                Array.Clear(_buffer, 0, _buffer.Length);
                using (var accessor = _mmf.CreateViewAccessor()) {
                    accessor.WriteArray(0, _buffer, 0, _buffer.Length);
                }
            }
            _mmf?.Dispose();
            base.EndRun();
        }

        protected override void UnloadContent() {
            base.UnloadContent();

            Logger.Debug("Unloading services.");

            // Let all of the game services have a chance to unload
            foreach (var service in GameService.All) {
                service.DoUnload();
            }
        }

        protected override void Update(GameTime gameTime) {
            if (!GameService.GameIntegration.Gw2Instance.Gw2IsRunning) {
                Form.Location = new System.Drawing.Point(-32000, -32000);

                // If gw2 isn't open so only run the essentials
                GameService.Debug.DoUpdate(gameTime);
                GameService.GameIntegration.DoUpdate(gameTime);
                GameService.Module.DoUpdate(gameTime);

                for (int i = 0; i < 200; i++) { // Wait ~10 seconds between checks
                    if (GameService.GameIntegration.Gw2Instance.Gw2IsRunning || GameService.Overlay.Exiting) break;
                    Thread.Sleep(50);
                    Application.DoEvents();
                }

                return;
            }

            // Update all game services
            foreach (var service in GameService.All) {
                GameService.Debug.StartTimeFunc($"Service: {service.GetType().Name}");
                service.DoUpdate(gameTime);
                GameService.Debug.StopTimeFunc($"Service: {service.GetType().Name}");
            }

            base.Update(gameTime);

            _drawLag += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        private float _drawLag;

        private bool _skipDraw = false;

        internal void SkipDraw() {
            _skipDraw = true;
        }

        internal void ReadRenderTargetContent(int pageLen) {
            if (_renderTexture == null) return;

            if (_buffer == null || _buffer.Length < pageLen) {
                _buffer = new Color[pageLen];
            }

            _renderTexture.GetData(_buffer, 0, pageLen);

            int chunkLen = pageLen / 8;

            using (var accessor = _mmf.CreateViewAccessor(0, pageLen * 4)) {
                if (accessor != null) {

                    Parallel.For(0, 8, (int iChunk) => {
                        accessor.WriteArray(iChunk * chunkLen * 4, _buffer, iChunk * chunkLen, chunkLen);
                    });
                }
            }
        }
        internal void RenderToTexture(GameTime gameTime) {
            using var ctx = GameService.Graphics.LendGraphicsDeviceContext();
            var width = ctx.GraphicsDevice.PresentationParameters.BackBufferWidth;
            var height = ctx.GraphicsDevice.PresentationParameters.BackBufferHeight;
            var pageLen = width * height;

            try {

                if (_renderTexture == null || _renderTexture.IsDisposed) {
                    Directory.CreateDirectory("tmp\\blishhud");
                    String mapname = "blishhud_" + (string.IsNullOrEmpty(ApplicationSettings.Instance.MumbleMapName) ? "" : $"{ApplicationSettings.Instance.MumbleMapName}") + "_" + width.ToString() + "x" + height.ToString();
                    Logger.Info("new rendertarget: " + mapname);
                    _renderTexture = new RenderTarget2D(ctx.GraphicsDevice, width,
                        height, false, SurfaceFormat.Bgr32, DepthFormat.Depth24, 1, RenderTargetUsage.PreserveContents);
                    _mmf = MemoryMappedFile.CreateFromFile("tmp\\blishhud\\" + mapname, FileMode.OpenOrCreate, mapname, pageLen * 4 * 2, MemoryMappedFileAccess.ReadWrite);
                }

                if (_renderTexture.Width != width || _renderTexture.Height != height) {
                    Logger.Info("remove rendertarget");
                    _renderTexture?.Dispose();
                    _mmf?.Dispose();
                    return;
                }

                ctx.GraphicsDevice.SetRenderTarget(_renderTexture);
                ctx.GraphicsDevice.DepthStencilState = new DepthStencilState() { DepthBufferEnable = true };
                GameService.Graphics.Render(gameTime, _basicSpriteBatch);
                ctx.GraphicsDevice.SetRenderTargets(null);

                ReadRenderTargetContent(pageLen);

            } catch (Exception ex) {
                Logger.Error(ex, ex.Message);
            }
        }

        protected override void Draw(GameTime gameTime) {
            if (_skipDraw) {
                Thread.Sleep(1);
                _skipDraw = false;
                return;
            }

            GameService.Debug.TickFrameCounter(_drawLag);
            _drawLag = 0;

            if (!GameService.GameIntegration.Gw2Instance.Gw2IsRunning) return;

            RenderToTexture(gameTime);

#if DEBUG
            GameService.Graphics.Render(gameTime, _basicSpriteBatch);

            _basicSpriteBatch.Begin();
            GameService.Debug.DrawDebugOverlay(_basicSpriteBatch, gameTime);
            _basicSpriteBatch.End();
#endif
            base.Draw(gameTime);
        }
    }
}

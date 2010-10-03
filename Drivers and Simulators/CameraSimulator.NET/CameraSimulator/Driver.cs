//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Camera driver for Simulator
//
// Description:	A very basic Camera simulator.
//
// Implements:	ASCOM Camera interface version: 1.0
// Author:		Bob Denny <rdenny@dc3.com>
//				using Matthias Busch's VB6 Camera Simulator and Chris Rowland's
//				C#.NET Camera Driver template.
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 14-Oct-2007	rbd	1.0.0	Initial edit, from ASCOM Camera Driver template
// 09-Jan-2009  cdr 6.0.0   Get the basic functionality working, some V2 properties in place but no interface
// 22-Jan-2009  cdr 6.0.0   More functionality, including temperature, noise, image
// 03-Oct-2010  cdr 6.0.0   Should be close to complete.
// --------------------------------------------------------------------------------
//
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ASCOM.DeviceInterface;
using ASCOM.Utilities;

namespace ASCOM.Simulator
{
    /// <summary>
    /// Your driver's ID is ASCOM.Simulator.Camera
	/// The Guid attribute sets the CLSID for ASCOM.Simulator.Camera
	/// The ClassInterface/None attribute prevents an empty interface called
	/// _Camera from being created and used as the [default] interface
    /// </summary>
	[Guid("12229c31-e7d6-49e8-9c5d-5d7ff05c3bfe"), ClassInterface(ClassInterfaceType.None),ComVisible(true)]
	public class Camera : ICameraV2
    {
        #region profile string constants
        private const string STR_InterfaceVersion = "InterfaceVersion";
        private const string STR_PixelSizeX = "PixelSizeX";
        private const string STR_PixelSizeY = "PixelSizeY";
        private const string STR_FullWellCapacity = "FullWellCapacity";
        private const string STR_MaxADU = "MaxADU";
        private const string STR_ElectronsPerADU = "ElectronsPerADU";
        private const string STR_CameraXSize = "CameraXSize";
        private const string STR_CameraYSize = "CameraYSize";
        private const string STR_CanAsymmetricBin = "CanAsymmetricBin";
        private const string STR_MaxBinX = "MaxBinX";
        private const string STR_MaxBinY = "MaxBinY";
        private const string STR_HasShutter = "HasShutter";
        private const string STR_SensorName = "SensorName";
        private const string STR_SensorType = "SensorType";
        private const string STR_BayerOffsetX = "BayerOffsetX";
        private const string STR_BayerOffsetY = "BayerOffsetY";
        private const string STR_HasCooler = "HasCooler";
        private const string STR_CanSetCCDTemperature = "CanSetCCDTemperature";
        private const string STR_CanGetCoolerPower = "CanGetCoolerPower";
        private const string STR_SetCCDTemperature = "SetCCDTemperature";
        private const string STR_CanAbortExposure = "CanAbortExposure";
        private const string STR_CanStopExposure = "CanStopExposure";
        private const string STR_MinExposure = "MinExposure";
        private const string STR_MaxExposure = "MaxExposure";
        private const string STR_ExposureResolution = "ExposureResolution";
        private const string STR_ImagePath = "ImageFile";
        private const string STR_ApplyNoise = "ApplyNoise";
        #endregion

        #region internal properties

        //Interface version
        internal short interfaceVersion;

        //Pixel
        internal double pixelSizeX;
        internal double pixelSizeY;
        internal double fullWellCapacity;
        internal int maxADU;
        internal double electronsPerADU;

        //CCD
        internal int cameraXSize;
        internal int cameraYSize;
        internal bool canAsymmetricBin;
        internal short maxBinX;
        internal short maxBinY;
        internal short binX;
        internal short binY;
        internal bool hasShutter;
        internal string sensorName;
        internal SensorType sensorType;    // TODO make an Enum
        internal short bayerOffsetX;
        internal short bayerOffsetY;

        internal int startX;
        internal int startY;
        internal int numX;
        internal int numY;

        //cooling
        internal bool hasCooler;
        private bool coolerOn;
        internal bool canSetCcdTemperature;
        internal bool canGetCoolerPower;
        internal double coolerPower;
        internal double ccdTemperature;
        internal double heatSinkTemperature;
        internal double setCcdTemperature;

        // Gain
        internal string[] gains;
        internal short gainMin;
        internal short gainMax;
        private short gain;

        // Exposure
        internal bool canAbortExposure;
        internal bool canStopExposure;
        internal double exposureMax;
        internal double exposureMin;
        internal double exposureResolution;
        private double lastExposureDuration;
        private string lastExposureStartTime;

        private DateTime exposureStartTime;
        private double exposureDuration;
        private bool imageReady = false;

        // readout
        internal bool canFastReadout;
        private bool fastReadout;
        private short readoutMode;
        internal string[] readoutModes;

        // simulation
        internal string imagePath;
        internal bool applyNoise;
        private float[,,] imageData;    // room for a 3 plane colour image
        private bool darkFrame;

        internal bool connected=false;
        internal CameraStates cameraState = CameraStates.cameraIdle;

        private int[,] imageArray;
        private object[,] imageArrayVariant;
        private int[,,] imageArrayColour;
        private object[,,] imageArrayVariantColour;
        private string lastError = string.Empty;

        private ASCOM.Utilities.Timer exposureTimer;
        private ASCOM.Utilities.Timer coolerTimer;

        #endregion

		#region Camera Constructor
		//
		// Driver ID and descriptive string that shows in the Chooser
		//
        private static string s_csDriverID = "ASCOM.Simulator.Camera";
		// TODO Change the descriptive string for your driver then remove this line
		private static string s_csDriverDescription = "Camera V2 simulator";
        /// <summary>
        /// Initializes a new instance of the <see cref="Camera"/> class.
        /// Must be public for COM registration!
        /// </summary>
		public Camera()
		{
			// TODO Implement your additional construction here
            InitialiseSimulator();
		}
		#endregion

		#region ASCOM Registration
		//
		// Register or unregister driver for ASCOM. This is harmless if already
		// registered or unregistered. 
		//
		private static void RegUnregASCOM(bool bRegister)
		{
			Profile P = new Profile();
			P.DeviceType = "Camera";					//  Requires Helper 5.0.3 or later
			if (bRegister)
				P.Register(s_csDriverID, s_csDriverDescription);
			else
				P.Unregister(s_csDriverID);
			try                        // In case Helper becomes native .NET
			{
				Marshal.ReleaseComObject(P);
			}
			catch (Exception) { }
			P = null;
		}

		[ComRegisterFunction]
		public static void RegisterASCOM(Type t)
		{
			RegUnregASCOM(true);
		}

		[ComUnregisterFunction]
		public static void UnregisterASCOM(Type t)
		{
			RegUnregASCOM(false);
		}
		#endregion

        //
		// PUBLIC COM INTERFACE ICamera IMPLEMENTATION
        //

        #region Common Methods
        public string[] SupportedActions
        {
            get { throw new MethodNotImplementedException("SupportedActions"); }
        }

        public void Dispose()
        {
        }

        public string CommandString(string Command, bool Raw)
        {
            throw new MethodNotImplementedException("CommandString");
        }

        public void CommandBlind(string Command, bool Raw)
        {
            throw new MethodNotImplementedException("CommandBlind");
        }
        public bool CommandBool(string Command, bool Raw)
        {
            throw new MethodNotImplementedException("CommandBool");
        }

        public string Action(string Command, string Parameters)
        {
            throw new MethodNotImplementedException("Action");
        }

        #endregion

        #region ICamera Members


        /// <summary>
		/// Aborts the current exposure, if any, and returns the camera to Idle state.
		/// Must throw exception if camera is not idle and abort is
		///  unsuccessful (or not possible, e.g. during download).
		/// Must throw exception if hardware or communications error
		///  occurs.
		/// Must NOT throw an exception if the camera is already idle.
		/// </summary>
		public void AbortExposure()
		{
            if (!this.connected)
                throw new NotConnectedException("Can't abort exposure when not connected");
            if (!this.canAbortExposure)
                throw new ASCOM.MethodNotImplementedException("AbortExposure");
            switch (this.cameraState)
            {
                case CameraStates.cameraWaiting:
                case CameraStates.cameraExposing:
                case CameraStates.cameraReading:
                case CameraStates.cameraDownload:
                    // these are all possible exposure states so we can abort the exposure
                    this.exposureTimer.Enabled = false;
                    this.cameraState = CameraStates.cameraIdle;
                    this.imageReady = false;
                    break;
                case CameraStates.cameraIdle:
                    break;
                case CameraStates.cameraError:
                    throw new ASCOM.InvalidOperationException("AbortExposure not possible because of an error");
            }
        }

		/// <summary>
		/// Sets the binning factor for the X axis.  Also returns the current value.  Should
		/// default to 1 when the camera link is established.  Note:  driver does not check
		/// for compatible subframe values when this value is set; rather they are checked
		/// upon StartExposure.
		/// </summary>
		/// <value>BinX sets/gets the X binning value</value>
		/// <exception>Must throw an exception for illegal binning values</exception>
		public short BinX
		{
			get
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't read BinX when not connected");
                return this.binX;
			}
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set BinX when not connected");
                if (value > this.maxBinX || value < 1)
                    throw new InvalidValueException("BinX", value.ToString("d2"), string.Format("1 to {0}", this.MaxBinX));
				this.binX = value;
                if (!this.canAsymmetricBin)
                    this.binY = value;
			}
		}

		/// <summary>
		/// Sets the binning factor for the Y axis  Also returns the current value.  Should
		/// default to 1 when the camera link is established.  Note:  driver does not check
		/// for compatible subframe values when this value is set; rather they are checked
		/// upon StartExposure.
		/// </summary>
		/// <exception>Must throw an exception for illegal binning values</exception>
		public short BinY
		{
			get
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't read BinY when not connected");
                return this.binY;
			}
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set BinY when not connected");
                if (value > this.maxBinY || value < 1)
                    throw new InvalidValueException("BinY", value.ToString("d2"), string.Format("1 to {0}", this.MaxBinY));
				this.binY = value;
                if (!this.canAsymmetricBin)
                    this.binX = value;
			}
		}

		/// <summary>
		/// Returns the current CCD temperature in degrees Celsius. Only valid if
		/// CanControlTemperature is True.
		/// </summary>
		/// <exception>Must throw exception if data unavailable.</exception>
		public double CCDTemperature
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read the CCD temperature when not connected");
                return this.ccdTemperature;
            }
		}

		/// <summary>
		/// Returns one of the following status information:
		/// <list type="bullet">
		///  <listheader>
		///   <description>Value  State          Meaning</description>
		///  </listheader>
		///  <item>
		///   <description>0      CameraIdle      At idle state, available to start exposure</description>
		///  </item>
		///  <item>
		///   <description>1      CameraWaiting   Exposure started but waiting (for shutter, trigger,
		///                        filter wheel, etc.)</description>
		///  </item>
		///  <item>
		///   <description>2      CameraExposing  Exposure currently in progress</description>
		///  </item>
		///  <item>
		///   <description>3      CameraReading   CCD array is being read out (digitized)</description>
		///  </item>
		///  <item>
		///   <description>4      CameraDownload  Downloading data to PC</description>
		///  </item>
		///  <item>
		///   <description>5      CameraError     Camera error condition serious enough to prevent
		///                        further operations (link fail, etc.).</description>
		///  </item>
		/// </list>
		/// </summary>
		/// <exception cref="System.Exception">Must return an exception if the camera status is unavailable.</exception>
		public CameraStates CameraState
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read the camera state when not connected");
                return this.cameraState;
            }
		}

		/// <summary>
		/// Returns the width of the CCD camera chip in unbinned pixels.
		/// </summary>
		/// <exception cref="System.Exception">Must throw exception if the value is not known</exception>
		public int CameraXSize
		{
			get
            { 
                if (!this.connected)
                    throw new NotConnectedException("Can't read the camera Xsize when not connected");
                return this.cameraXSize;
            }
		}

		/// <summary>
		/// Returns the height of the CCD camera chip in unbinned pixels.
		/// </summary>
		/// <exception cref="System.Exception">Must throw exception if the value is not known</exception>
		public int CameraYSize
		{
			get
            { 
                if (!this.connected)
                    throw new NotConnectedException("Can't read the camera Ysize when not connected");
                return this.cameraYSize;
            }
		}

		/// <summary>
		/// Returns True if the camera can abort exposures; False if not.
		/// </summary>
		public bool CanAbortExposure
		{
			get 
            { 
                if (!this.connected)
                    throw new NotConnectedException("Can't read CanAbortExposure when not connected");
                return this.canAbortExposure; 
            }
		}

		/// <summary>
		/// If True, the camera can have different binning on the X and Y axes, as
		/// determined by BinX and BinY. If False, the binning must be equal on the X and Y
		/// axes.
		/// </summary>
		/// <exception cref="System.Exception">Must throw exception if the value is not known (n.b. normally only
		///            occurs if no link established and camera must be queried)</exception>
		public bool CanAsymmetricBin
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read CanAsymmetricBin when not connected");
                return this.canAsymmetricBin;
            }
        }

		/// <summary>
		/// If True, the camera's cooler power setting can be read.
		/// </summary>
        public bool CanGetCoolerPower
        {
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read CanGetCoolerPower when not connected");
                return this.canGetCoolerPower;
            }
        }

		/// <summary>
		/// Returns True if the camera can send autoguider pulses to the telescope mount;
		/// False if not.  (Note: this does not provide any indication of whether the
		/// autoguider cable is actually connected.)
		/// </summary>
		public bool CanPulseGuide
		{
			get { return false; }
		}

		/// <summary>
		/// If True, the camera's cooler setpoint can be adjusted. If False, the camera
		/// either uses open-loop cooling or does not have the ability to adjust temperature
		/// from software, and setting the TemperatureSetpoint property has no effect.
		/// </summary>
		public bool CanSetCCDTemperature
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read CanSetCCDTemperature when not connected");
                return this.canSetCcdTemperature;
            }
        }

		/// <summary>
		/// Some cameras support StopExposure, which allows the exposure to be terminated
		/// before the exposure timer completes, but will still read out the image.  Returns
		/// True if StopExposure is available, False if not.
		/// </summary>
		/// <exception cref=" System.Exception">not supported</exception>
		/// <exception cref=" System.Exception">an error condition such as link failure is present</exception>
		public bool CanStopExposure
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read CanStopExposure when not connected");
                return this.canStopExposure;
            }
		}

		/// <summary>
		/// Controls the link between the driver and the camera. Set True to enable the
		/// link. Set False to disable the link (this does not switch off the cooler).
		/// You can also read the property to check whether it is connected.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if unsuccessful.</exception>
		public bool Connected
		{
			get
			{
				return this.connected;
			}
			set
			{
                if (value)
                    ReadImageFile();
                this.connected = value;
			}
		}

		/// <summary>
		/// Turns on and off the camera cooler, and returns the current on/off state.
		/// Warning: turning the cooler off when the cooler is operating at high delta-T
		/// (typically >20C below ambient) may result in thermal shock.  Repeated thermal
		/// shock may lead to damage to the sensor or cooler stack.  Please consult the
		/// documentation supplied with the camera for further information.
		/// </summary>
		/// <exception cref=" System.Exception">not supported</exception>
		/// <exception cref=" System.Exception">an error condition such as link failure is present</exception>
		public bool CoolerOn
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read CoolerOn when not connected");
                if (!this.hasCooler)
                    throw new PropertyNotImplementedException("CoolerOn", false);
                return this.coolerOn;
            }
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set CoolerOn when not connected");
                if (!this.hasCooler)
                    throw new PropertyNotImplementedException("CoolerOn", true);
                this.coolerOn = value;

                if (this.canSetCcdTemperature)
                {
                    // implement CCD temperature control
                    if (this.coolerTimer == null)
                    {
                        coolerTimer = new ASCOM.Utilities.Timer();
                        coolerTimer.Tick += new ASCOM.Utilities.Timer.TickEventHandler(coolerTimer_Tick);
                        coolerTimer.Interval = 1000;
                        coolerTimer.Enabled = true;
                    }
                }
			}
		}

        /// <summary>
        /// Adjust the ccd temperature and power once a second
        /// </summary>
        private void  coolerTimer_Tick()
        {
            if (this.coolerOn && this.canSetCcdTemperature)
            {
                this.coolerPower = Math.Min(100, Math.Max((this.ccdTemperature - this.setCcdTemperature) * 100, 0));
                this.ccdTemperature -= (this.coolerPower / 50);     // reduce temperature by up to 2 deg per sec.
            }
            // increase temperature by 2 deg per sec at a differential of 40
            this.ccdTemperature += (this.heatSinkTemperature - this.ccdTemperature)/20.0;
        }
        
        /// <summary>
		/// Returns the present cooler power level, in percent.  Returns zero if CoolerOn is
		/// False.
		/// </summary>
		/// <exception cref=" System.Exception">not supported</exception>
		/// <exception cref=" System.Exception">an error condition such as link failure is present</exception>
		public double CoolerPower
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read Cooler Power when not connected");
                if (!this.hasCooler)
                    throw new PropertyNotImplementedException("CoolerPower", false);
                return this.coolerPower;
            }
        }

		/// <summary>
		/// Returns a description of the camera model, such as manufacturer and model
		/// number. Any ASCII characters may be used. The string shall not exceed 68
		/// characters (for compatibility with FITS headers).
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if description unavailable</exception>
		public string Description
		{
			get 
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read Description when not connected");
                if (this.interfaceVersion == 1)
                    return "Simulated V1 Camera";
                else
                    return string.Format("Simulated {0} camera {1}", this.sensorType, this.SensorName);
            }
		}

		/// <summary>
		/// Returns the gain of the camera in photoelectrons per A/D unit. (Some cameras have
		/// multiple gain modes; these should be selected via the SetupDialog and thus are
		/// static during a session.)
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public double ElectronsPerADU
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read ElectronsPerADU when not connected");
                return this.electronsPerADU;
            }
        }

		/// <summary>
		/// Reports the full well capacity of the camera in electrons, at the current camera
		/// settings (binning, SetupDialog settings, etc.)
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public double FullWellCapacity
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read FullWellCapacity when not connected");
                return this.fullWellCapacity;
            }
		}

		/// <summary>
		/// If True, the camera has a mechanical shutter. If False, the camera does not have
		/// a shutter.  If there is no shutter, the StartExposure command will ignore the
		/// Light parameter.
		/// </summary>
		public bool HasShutter
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read HasShutter when not connected");
                return this.hasShutter;
            }
        }

		/// <summary>
		/// Returns the current heat sink temperature (called "ambient temperature" by some
		/// manufacturers) in degrees Celsius. Only valid if CanControlTemperature is True.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public double HeatSinkTemperature
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read HeatSinkTemperature when not connected");
                if (!this.canSetCcdTemperature)
                    throw new PropertyNotImplementedException("HeatSinkTemperature", false);
                return this.heatSinkTemperature;
            }
        }

		/// <summary>
		/// Returns a safearray of int of size NumX * NumY containing the pixel values from
		/// the last exposure. The application must inspect the Safearray parameters to
		/// determine the dimensions. Note: if NumX or NumY is changed after a call to
		/// StartExposure it will have no effect on the size of this array. This is the
		/// preferred method for programs (not scripts) to download images since it requires
		/// much less memory.
		///
		/// For color or multispectral cameras, will produce an array of NumX * NumY *
		/// NumPlanes.  If the application cannot handle multispectral images, it should use
		/// just the first plane.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public object ImageArray
		{
			get 
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read ImageArray when not connected");
                if (!this.imageReady)
                    throw new ASCOM.InvalidOperationException("There is no image available");

                if (this.sensorType == SensorType.Color)
                    return this.imageArrayColour;
                else
                    return this.imageArray;
            }
		}

		/// <summary>
		/// Returns a safearray of Variant of size NumX * NumY containing the pixel values
		/// from the last exposure. The application must inspect the Safearray parameters to
		/// determine the dimensions. Note: if NumX or NumY is changed after a call to
		/// StartExposure it will have no effect on the size of this array. This property
		/// should only be used from scripts due to the extremely high memory utilization on
		/// large image arrays (26 bytes per pixel). Pixels values should be in Short, int,
		/// or Double format.
		///
		/// For color or multispectral cameras, will produce an array of NumX * NumY *
		/// NumPlanes.  If the application cannot handle multispectral images, it should use
		/// just the first plane.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public object ImageArrayVariant
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read ImageArrayVariant when not connected");
                if (!this.imageReady)
                    throw new ASCOM.InvalidOperationException("There is no image available");
                // convert to variant
                if (this.sensorType == SensorType.Color)
                {
                    this.imageArrayVariantColour = new object[imageArrayColour.GetLength(0), imageArrayColour.GetLength(1), 3];
                    for (int i = 0; i < imageArrayColour.GetLength(1); i++)
                    {
                        for (int j = 0; j < imageArrayColour.GetLength(0); j++)
                        {
                            for (int k = 0; k < 3; k++)
                                imageArrayVariantColour[j, i, k] = imageArrayColour[j, i, k];
                        }

                    }
                    return imageArrayVariantColour;
                }
                else
                {
                    this.imageArrayVariant = new object[imageArray.GetLength(0), imageArray.GetLength(1)];
                    for (int i = 0; i < imageArray.GetLength(1); i++)
                    {
                        for (int j = 0; j < imageArray.GetLength(0); j++)
                        {
                            imageArrayVariant[j, i] = imageArray[j, i];
                        }

                    }
                    return imageArrayVariant;
                }
            }
		}

		/// <summary>
		/// If True, there is an image from the camera available. If False, no image
		/// is available and attempts to use the ImageArray method will produce an
		/// exception.
		/// </summary>
		/// <exception cref=" System.Exception">hardware or communications link error has occurred.</exception>
		public bool ImageReady
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read ImageReady when not connected");
                return this.imageReady;
            }
        }

		/// <summary>
		/// If True, pulse guiding is in progress. Required if the PulseGuide() method
		/// (which is non-blocking) is implemented. See the PulseGuide() method.
		/// </summary>
		/// <exception cref=" System.Exception">hardware or communications link error has occurred.</exception>
		public bool IsPulseGuiding
		{
			get { throw new System.Exception("The method or operation is not implemented."); }
		}

		/// <summary>
		/// Reports the actual exposure duration in seconds (i.e. shutter open time).  This
		/// may differ from the exposure time requested due to shutter latency, camera timing
		/// precision, etc.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if not supported or no exposure has been taken</exception>
		public double LastExposureDuration
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read LastExposureDuration when not connected");
                if (!this.imageReady)
                    throw new NotConnectedException("Can't read LastExposureDuration when no image is ready");
                return this.lastExposureDuration;
            }
        }

		/// <summary>
		/// Reports the actual exposure start in the FITS-standard
		/// CCYY-MM-DDThh:mm:ss[.sss...] format.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if not supported or no exposure has been taken</exception>
		public string LastExposureStartTime
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read LastExposureStartTime when not connected");
                if (!this.imageReady)
                    throw new NotConnectedException("Can't read LastExposureStartTime when no image is ready");
                return this.lastExposureStartTime;
            }
		}

		/// <summary>
		/// Reports the maximum ADU value the camera can produce.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public int MaxADU
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read MaxADU when not connected");
                return this.maxADU;
            }
		}

		/// <summary>
		/// If AsymmetricBinning = False, returns the maximum allowed binning factor. If
		/// AsymmetricBinning = True, returns the maximum allowed binning factor for the X axis.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public short MaxBinX
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read MaxBinX when not connected");
                return this.maxBinX;
            }
        }

		/// <summary>
		/// If AsymmetricBinning = False, equals MaxBinX. If AsymmetricBinning = True,
		/// returns the maximum allowed binning factor for the Y axis.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public short MaxBinY
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read MaxBinY when not connected");
                return this.maxBinY;
            }
        }

		/// <summary>
		/// Sets the subframe width. Also returns the current value.  If binning is active,
		/// value is in binned pixels.  No error check is performed when the value is set.
		/// Should default to CameraXSize.
		/// </summary>
		public int NumX
		{
			get
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't read NumX when not connected");
                return this.numX;
			}
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set NumX when not connected");
                this.numX = value;
			}
		}

		/// <summary>
		/// Sets the subframe height. Also returns the current value.  If binning is active,
		/// value is in binned pixels.  No error check is performed when the value is set.
		/// Should default to CameraYSize.
		/// </summary>
		public int NumY
		{
			get
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't read NumY when not connected");
                return this.numY;
            }
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set NumY when not connected");
                this.numY = value;
			}
		}

		/// <summary>
		/// Returns the width of the CCD chip pixels in microns, as provided by the camera
		/// driver.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public double PixelSizeX
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read PixelSizeX when not connected");
                return this.pixelSizeX;
            }
        }

		/// <summary>
		/// Returns the height of the CCD chip pixels in microns, as provided by the camera
		/// driver.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if data unavailable.</exception>
		public double PixelSizeY
		{
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read PixelSizeY when not connected");
                return this.pixelSizeY;
            }
        }

		/// <summary>
		/// This method returns only after the move has completed.
		///
		/// symbolic Constants
		/// The (symbolic) values for GuideDirections are:
		/// Constant     Value      Description
		/// --------     -----      -----------
		/// guideNorth     0        North (+ declination/elevation)
		/// guideSouth     1        South (- declination/elevation)
		/// guideEast      2        East (+ right ascension/azimuth)
		/// guideWest      3        West (+ right ascension/azimuth)
		///
		/// Note: directions are nominal and may depend on exact mount wiring.  guideNorth
		/// must be opposite guideSouth, and guideEast must be opposite guideWest.
		/// </summary>
		/// <param name="Direction">Direction of guide command</param>
		/// <param name="Duration">Duration of guide in milliseconds</param>
		/// <exception cref=" System.Exception">PulseGuide command is unsupported</exception>
		/// <exception cref=" System.Exception">PulseGuide command is unsuccessful</exception>
		public void PulseGuide(GuideDirections Direction, int Duration)
		{
			throw new System.Exception("The method or operation is not implemented.");
		}

		/// <summary>
		/// Sets the camera cooler setpoint in degrees Celsius, and returns the current
		/// setpoint.
		/// Note:  camera hardware and/or driver should perform cooler ramping, to prevent
		/// thermal shock and potential damage to the CCD array or cooler stack.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw exception if command not successful.</exception>
		/// <exception cref=" System.Exception">Must throw exception if CanSetCCDTemperature is False.</exception>
		public double SetCCDTemperature
		{
			get
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't read SetCCDTemperature when not connected");
                if (!this.canSetCcdTemperature)
                    throw new PropertyNotImplementedException(STR_SetCCDTemperature, false);
                return this.setCcdTemperature;
            }
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set SetCCDTemperature when not connected");
                if (!this.canSetCcdTemperature)
                    throw new PropertyNotImplementedException(STR_SetCCDTemperature, true);
                if (value < -50 || value > 20)
                    throw new InvalidValueException("SetCCDTemperature", value.ToString(), "-50 to 20");
                this.setCcdTemperature = value;
                // does this turn cooling on?
			}
		}

		/// <summary>
		/// Launches a configuration dialog box for the driver.  The call will not return
		/// until the user clicks OK or cancel manually.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw an exception if Setup dialog is unavailable.</exception>
		public void SetupDialog()
		{
            if (this.connected)
                throw new NotConnectedException("Can't set the CCD properties when connected");
			SetupDialogForm F = new SetupDialogForm();
            F.InitProperties(this);
			F.ShowDialog();
            if (F.okButtonPressed)
                this.SaveToProfile();
		}

        /// <summary>
        /// Starts an exposure. Use ImageReady to check when the exposure is complete.
        /// </summary>
        /// <param name="Duration">exxposure duration in seconds</param>
        /// <param name="Light">True if light frame, only valid if the camera has a shutter</param>
        /// <exception cref=" System.Exception">NumX, NumY, XBin, YBin, StartX, StartY, or Duration parameters are invalid.</exception>
        /// <exception cref=" System.Exception">CanAsymmetricBin is False and BinX != BinY</exception>
        /// <exception cref=" System.Exception">the exposure cannot be started for any reason, such as a hardware or communications error</exception>
        public void StartExposure(double Duration, bool Light)
		{
            if (!this.connected)
                throw new NotConnectedException("Can't set StartExposure when not connected");
            // check the duration
            if (Duration > this.exposureMax || Duration < this.exposureMin)
            {
                this.lastError="Incorrect exposure duration";
                throw new ASCOM.InvalidValueException("StartExposure Duration",
                                                     Duration.ToString(),
                                                     string.Format("{0} to {1}", this.exposureMax, this.exposureMin));
            }
            //  binning tests
            if ((this.binX > this.MaxBinX) || (this.BinX < 1) )
            {
                this.lastError="Incorrect bin X factor";
                throw new ASCOM.InvalidValueException("StartExposure BinX",
                                                    this.binX.ToString(),
                                                    string.Format("1 to {0}",this.maxBinX));
            }
            if ((this.binY > this.MaxBinY) || (this.BinY < 1) )
            {
                this.lastError="Incorrect bin Y factor";
                throw new ASCOM.InvalidValueException("StartExposure BinY",
                                                    this.binY.ToString(),
                                                    string.Format("1 to {0}",this.maxBinY));
            }
            // check the start position is in range
            // start is in binned pixels
            if (this.startX < 0 || this.startX * this.binX > this.cameraXSize)
            {
                this.lastError="Incorrect Start X position";
                throw new ASCOM.InvalidValueException("StartExposure StartX",
                                                    this.startX.ToString(),
                                                    string.Format("0 to {0}",cameraXSize/this.binX));
            }
            if (this.startY < 0 || this.startY * this.binY > this.cameraYSize)
            {
                this.lastError="Incorrect Start X position";
                throw new ASCOM.InvalidValueException("StartExposure StartX",
                                                    this.startX.ToString(),
                                                    string.Format("0 to {0}",cameraXSize/this.binX));
            }
            // check that the acquisition is at least 1 pixel in size and fits in the camera area
            if (this.numX < 1 || (this.numX + this.startX ) * this.binX > this.cameraXSize)
            {
                this.lastError="Incorrect Num X value";
                throw new ASCOM.InvalidValueException("StartExposure NumX",
                                                    this.numX.ToString(),
                                                    string.Format("1 to {0}",cameraXSize/this.binX));
            }
            if (this.numY < 1 || (this.numY + this.startY ) * this.binY > this.cameraYSize)
            {
                this.lastError="Incorrect Num Y value";
                throw new ASCOM.InvalidValueException("StartExposure NumY",
                                                    this.numY.ToString(),
                                                    string.Format("1 to {0}",cameraYSize/this.binY));
            }

            // set up the things to do at the start of the exposure
            this.imageReady = false;
            if (this.HasShutter)
            {
                this.darkFrame = !Light;
            }
            this.lastExposureStartTime = DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss");
            // set the image array dimensions
            if (this.SensorType == SensorType.Color)
                this.imageArrayColour = new int[this.numX, this.numY, 3];
            else
                this.imageArray = new int[this.numX, this.numY];

            if (this.exposureTimer == null)
            {
                this.exposureTimer = new ASCOM.Utilities.Timer();
                this.exposureTimer.Tick += exposureTimer_Tick;
            }
            this.exposureTimer.Interval = (int)(Duration * 1000);
            this.cameraState = CameraStates.cameraExposing;
            this.exposureStartTime = DateTime.Now;
            this.exposureDuration = Duration;
            this.exposureTimer.Enabled = true;
		}

        private void  exposureTimer_Tick()
        {
            this.exposureTimer.Enabled = false;
            this.lastExposureDuration = (DateTime.Now - this.exposureStartTime).TotalSeconds;
            this.cameraState = CameraStates.cameraDownload;
            this.FillImageArray();
            this.imageReady = true;
            this.cameraState = CameraStates.cameraIdle;
        }

		/// <summary>
		/// Sets the subframe start position for the X axis (0 based). Also returns the
		/// current value.  If binning is active, value is in binned pixels.
		/// </summary>
		public int StartX
		{
			get
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't read StartX when not connected");
                return this.startX;
            }
			set
			{
                if (!this.connected)
                    throw new NotConnectedException("Can't set StartX when not connected");
                this.startX = value;
			}
		}

		/// <summary>
		/// Sets the subframe start position for the Y axis (0 based). Also returns the
		/// current value.  If binning is active, value is in binned pixels.
		/// </summary>
        public int StartY
        {
            get
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't read StartY when not connected");
                return this.startY;
            }
            set
            {
                if (!this.connected)
                    throw new NotConnectedException("Can't set StartY when not connected");
                this.startY = value;
            }
        }

		/// <summary>
		/// Stops the current exposure, if any.  If an exposure is in progress, the readout
		/// process is initiated.  Ignored if readout is already in process.
		/// </summary>
		/// <exception cref=" System.Exception">Must throw an exception if CanStopExposure is False</exception>
		/// <exception cref=" System.Exception">Must throw an exception if no exposure is in progress</exception>
		/// <exception cref=" System.Exception">Must throw an exception if the camera or link has an error condition</exception>
		/// <exception cref=" System.Exception">Must throw an exception if for any reason no image readout will be available.</exception>
		public void StopExposure()
		{
            if (!this.connected)
                throw new NotConnectedException("Can't stop exposure when not connected");
            if (!this.canStopExposure)
                throw new ASCOM.MethodNotImplementedException("StopExposure");
            switch (this.cameraState)
            {
                case CameraStates.cameraWaiting:
                case CameraStates.cameraExposing:
                case CameraStates.cameraReading:
                case CameraStates.cameraDownload:
                    // these are all possible exposure states so we can stop the exposure
                    this.exposureTimer.Enabled = false;
                    this.lastExposureDuration = (DateTime.Now - this.exposureStartTime).TotalSeconds;
                    this.FillImageArray();
                    this.cameraState = CameraStates.cameraIdle;
                    this.imageReady = true;
                    break;
                case CameraStates.cameraIdle:
                    break;
                case CameraStates.cameraError:
                default:
                    // these states are this where it isn't possible to stop an exposure 
                    throw new ASCOM.InvalidOperationException("StopExposure not possible if not exposing");
            }
		}

		#endregion

        #region ICameraV2 properties

        /// <summary>
        /// Returns the X offset of the Bayer matrix, as defined in <see cref=""SensorType/>.
        /// Value returned must be in the range 0 to M-1, where M is the width of the Bayer matrix.
        /// The offset is relative to the 0,0 pixel in the sensor array, and does not change to reflect
        /// subframe settings. 
        /// </summary>
        /// <value>The bayer offset X.</value>
        public short BayerOffsetX
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read BayerOffsetX when not connected");
                return this.bayerOffsetX;
            }
        }

        /// <summary>
        /// Returns the Y offset of the Bayer matrix, as defined in <see cref=""SensorType/>.
        /// Value returned must be in the range 0 to M-1, where M is the height of the Bayer matrix.
        /// The offset is relative to the 0,0 pixel in the sensor array, and does not change to reflect
        /// subframe settings. 
        /// </summary>
        /// <value>The bayer offset Y.</value>
        public short BayerOffsetY
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read BayerOffsetY when not connected");
                return this.bayerOffsetY;
            }
        }

        /// <summary>
        /// If True, the <see cref="FastReadout"/> function is available. 
        /// </summary>
        /// <value>
        /// 	<c>true</c> if the <see cref="FastReadout"/> function is available; otherwise, <c>false</c>.
        /// </value>
        public bool CanFastReadout
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read CanFastReadout when not connected");
                return this.canFastReadout;
            }
        }

        /// <summary>
        /// Returns descriptive and version information about this ASCOM Camera driver.
        /// This string may contain line endings and may be hundreds to thousands of characters long.
        /// It is intended to display detailed information on the ASCOM driver, including version
        /// and copyright data.. See the <see cref="Description"/> property for descriptive info on the camera itself.
        /// To get the driver version for compatibility reasons, use the <see cref="InterfaceVersion"/> property. 
        /// </summary>
        /// <value>The driver info string</value>
        public string DriverInfo
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                String strVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                return (s_csDriverDescription + " - Version " + strVersion);
            }
        }

        /// <summary>
        /// A string containing only the major and minor version of the driver. This must be in the form "n.n".
        /// Not to be confused with the InterfaceVersion property, which is the version of this specification
        /// supported by the driver (currently 2). 
        /// </summary>
        public string DriverVersion
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return String.Format("{0}.{1}", version.Major, version.Minor);
            }
        }

        /// <summary>
        /// Returns the maximum exposure time in seconds supported by <see cref="StartExposure"/>. 
        /// </summary>
        /// <value>The max exposure.</value>
        public double ExposureMax
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read ExposureMax when not connected");
                return this.exposureMax;
            }
        }

        /// <summary>
        /// Returns the minimum exposure time in seconds supported by <see cref="StartExposure"/>. 
        /// </summary>
        /// <value>The min exposure.</value>
        public double ExposureMin
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read ExposureMin when not connected");
                return this.exposureMin;
            }
        }

        /// <summary>
        /// Returns the smallest increment of exposure time supported by <see cref="StartExposure"/>.
        /// </summary>
        /// <value>The exposure resolution.</value>
        public double ExposureResolution
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read ExposureResolution when not connected");
                return this.exposureResolution;
            }
        }

        /// <summary>
        /// When set to True, the camera will operate in Fast mode; when set False,
        /// the camera will operate normally. This property should default to False. 
        /// </summary>
        /// <value><c>true</c> if [fast readout]; otherwise, <c>false</c>.</value>
        public bool FastReadout
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't read FastReadout when not connected");
                if (!this.canFastReadout)
                    throw new ASCOM.PropertyNotImplementedException("FastReadout", false);
                return this.fastReadout;
            }
            set
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't set FastReadout when not connected");
                if (!this.canFastReadout)
                    throw new ASCOM.PropertyNotImplementedException("FastReadout", true);
                this.fastReadout = value;
            }
        }

        /// <summary>
        /// Camera.Gain can be used to adjust the gain setting of the camera, if supported.
        /// The Gain, Gains, GainMin and GainMax operation is complex adjust this at your peril!
        /// </summary>
        /// <value>The gain.</value>
        public short Gain
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get Gain when not connected");
                if (this.gainMax <= this.gainMin)
                    throw new ASCOM.PropertyNotImplementedException("Gain", false);
                return this.gain;
            }
            set
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't set Gain when not connected");
                if (this.gainMax <= this.gainMin)
                    throw new ASCOM.PropertyNotImplementedException("Gain", true);
                if (value < this.gainMin || value > this.gainMax)
                    throw new ASCOM.InvalidValueException("Gain", value.ToString(), string.Format("{0} to {1}", this.gainMin, this.gainMax));
                this.gain = value;
            }
        }

        /// <summary>
        /// When specifying the gain setting with an integer value, Camera.GainMax is used
        /// in conjunction with Camera.GainMin to specify the range of valid settings.
        /// </summary>
        /// <value>The max gain.</value>
        public short GainMax
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get GainMax when not connected");
                if (this.gainMax <= this.gainMin)
                    throw new ASCOM.PropertyNotImplementedException("GainMax", false);
                if (this.gains != null && this.gains.Length > 0)
                    throw new ASCOM.InvalidOperationException("GainMax cannot be read if there is an array of Gains in use");
                return this.gainMax;
            }
        }

        /// <summary>
        /// When specifying the gain setting with an integer value, Camera.GainMin is used
        /// in conjunction with Camera.GainMax to specify the range of valid settings.
        /// </summary>
        /// <value>The min gain.</value>
        public short GainMin
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get GainMin when not connected");
                if (this.gainMax <= this.gainMin)
                    throw new ASCOM.PropertyNotImplementedException("GainMin", false);
                if (this.gains != null && this.gains.Length > 0)
                    throw new ASCOM.InvalidOperationException("GainMin cannot be read if there is an array of Gains in use");
                return this.gainMin;
            }
        }

        /// <summary>
        /// Gains provides a 0-based array of available gain settings.
        /// The Gain, Gains, GainMin and GainMax operation is complex adjust this at your peril!
        /// </summary>
        /// <value>The gains.</value>
        public string[] Gains
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get Gains when not connected");
                if (this.Gains == null || this.gains.Length == 0)
                    throw new ASCOM.PropertyNotImplementedException("Gains", false);
                return this.gains;
            }
        }

        /// <summary>
        /// Reports the version of this interface. Will return 2 for this version.
        /// </summary>
        /// <value>The interface version.</value>
        public short InterfaceVersion
        {
            get { return this.interfaceVersion; }
        }

        /// <summary>
        /// The short name of the camera, for display purposes.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get camera Name when not connected");
                return "Sim " + this.SensorName;
            }
        }

        /// <summary>
        /// If valid, returns an integer between 0 and 100, where 0 indicates 0% progress
        /// (function just started) and 100 indicates 100% progress (i.e. completion). 
        /// </summary>
        /// <value>The percent completed.</value>
        public short PercentCompleted
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get PercentCompleted when not connected");
                switch (this.cameraState)
                {
                    case CameraStates.cameraWaiting:
                    case CameraStates.cameraExposing:
                    case CameraStates.cameraReading:
                    case CameraStates.cameraDownload:
                        return (short)(((DateTime.Now - this.exposureStartTime).TotalSeconds / this.exposureDuration) * 100);
                    default:
                        throw new ASCOM.InvalidOperationException("get PercentCompleted is not valid if the camera is not active");
                }
            }
        }

        /// <summary>
        /// ReadoutMode is an index into the array <see cref="ReadoutModes"/>, and selects
        /// the desired readout mode for the camera.
        /// Defaults to 0 if not set.  Throws an exception if the selected mode is not available
        /// </summary>
        /// <value>The readout mode.</value>
        public short ReadoutMode
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get ReadoutMode when not connected");
                if (this.readoutModes == null || this.readoutModes.Length < 1)
                    throw new ASCOM.PropertyNotImplementedException("ReadoutMode", false);
                return this.readoutMode;
            }
            set
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't set ReadoutMode when not connected");
                if (this.readoutModes == null || this.readoutModes.Length < 1)
                    throw new ASCOM.PropertyNotImplementedException("ReadoutMode", true);
                if (value < this.readoutModes.GetLowerBound(0) || value > this.readoutModes.GetUpperBound(0))
                    throw new ASCOM.InvalidValueException("ReadoutMode", value.ToString(), string.Format("{0} to {1}", this.readoutModes.GetLowerBound(0), this.readoutModes.GetUpperBound(0)));
                this.readoutMode = value;
            }
        }


        /// <summary>
        /// This property provides an array of strings, each of which describes an available readout mode
        /// of the camera. 
        /// </summary>
        /// <value>The readout modes.</value>
        public string[] ReadoutModes
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get ReadoutModes when not connected");
                if (this.readoutModes == null || this.readoutModes.Length < 1)
                    throw new ASCOM.PropertyNotImplementedException("ReadoutModes", false);
                return this.readoutModes;
            }
        }

        /// <summary>
        /// Returns the name (datasheet part number) of the sensor, e.g. ICX285AL.
        /// The format is to be exactly as shown on manufacturer data sheet, subject to the following rules.
        /// All letter shall be uppercase.  Spaces shall not be included.
        /// </summary>
        /// <value>The name of the sensor.</value>
        public string SensorName
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get SensorName when not connected");
                return this.sensorName;
            }
        }

        /// <summary>
        /// SensorType returns a value indicating whether the sensor is monochrome,
        /// or what Bayer matrix it encodes. 
        /// </summary>
        /// <value>The type of the sensor.</value>
        public SensorType SensorType
        {
            get
            {
                if (interfaceVersion == 1)
                    throw new System.NotImplementedException();
                if (!this.connected)
                    throw new NotConnectedException("Can't get SensorType when not connected");
                return this.sensorType;
            }
        }

        #endregion

        #region private

        private void ReadFromProfile()
        {
            Profile profile = new Profile(true);
            profile.DeviceType = "Camera";
            // read properties from profile
            this.interfaceVersion = Convert.ToInt16(profile.GetValue(s_csDriverID, STR_InterfaceVersion, string.Empty, "2"));
            this.pixelSizeX = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_PixelSizeX, string.Empty, "5.6"), CultureInfo.InvariantCulture);
            this.pixelSizeY = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_PixelSizeY, string.Empty, "5.6"), CultureInfo.InvariantCulture);
            this.fullWellCapacity = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_FullWellCapacity, string.Empty, "30000"), CultureInfo.InvariantCulture);
            this.maxADU = Convert.ToInt32(profile.GetValue(s_csDriverID, STR_MaxADU, string.Empty, "65535"), CultureInfo.InvariantCulture);
            this.electronsPerADU = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_ElectronsPerADU, string.Empty, "0.8"), CultureInfo.InvariantCulture);

            this.cameraXSize = Convert.ToInt32(profile.GetValue(s_csDriverID, STR_CameraXSize, string.Empty, "800"), CultureInfo.InvariantCulture);
            this.cameraYSize = Convert.ToInt32(profile.GetValue(s_csDriverID, STR_CameraYSize, string.Empty, "600"), CultureInfo.InvariantCulture);
            this.canAsymmetricBin = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_CanAsymmetricBin, string.Empty, "true"), CultureInfo.InvariantCulture);
            this.maxBinX = Convert.ToInt16(profile.GetValue(s_csDriverID, STR_MaxBinX, string.Empty, "4"), CultureInfo.InvariantCulture);
            this.maxBinY = Convert.ToInt16(profile.GetValue(s_csDriverID, STR_MaxBinY, string.Empty, "4"), CultureInfo.InvariantCulture);
            this.hasShutter = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_HasShutter, string.Empty, "false"), CultureInfo.InvariantCulture);
            this.sensorName = profile.GetValue(s_csDriverID, STR_SensorName, string.Empty, "");
            this.sensorType = (ASCOM.DeviceInterface.SensorType)Convert.ToInt32(profile.GetValue(s_csDriverID, STR_SensorType, string.Empty, "0"), CultureInfo.InvariantCulture);
            this.bayerOffsetX = Convert.ToInt16(profile.GetValue(s_csDriverID, STR_BayerOffsetX, string.Empty, "0"), CultureInfo.InvariantCulture);
            this.bayerOffsetY = Convert.ToInt16(profile.GetValue(s_csDriverID, STR_BayerOffsetY, string.Empty, "0"), CultureInfo.InvariantCulture);

            this.hasCooler = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_HasCooler, string.Empty, "true"), CultureInfo.InvariantCulture);
            this.canSetCcdTemperature = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_CanSetCCDTemperature, string.Empty, "false"), CultureInfo.InvariantCulture);
            this.canGetCoolerPower = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_CanGetCoolerPower, string.Empty, "false"), CultureInfo.InvariantCulture);
            this.setCcdTemperature = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_SetCCDTemperature, string.Empty, "-10"), CultureInfo.InvariantCulture);

            this.canAbortExposure = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_CanAbortExposure, string.Empty, "true"), CultureInfo.InvariantCulture);
            this.canStopExposure = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_CanStopExposure, string.Empty, "true"), CultureInfo.InvariantCulture);
            this.exposureMax = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_MaxExposure, string.Empty, "3600"), CultureInfo.InvariantCulture);
            this.exposureMin = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_MinExposure, string.Empty, "0.001"), CultureInfo.InvariantCulture);
            this.exposureResolution = Convert.ToDouble(profile.GetValue(s_csDriverID, STR_ExposureResolution, string.Empty, "0.001"), CultureInfo.InvariantCulture);
            string fullPath = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly((this.GetType())).Location);
            this.imagePath = profile.GetValue(s_csDriverID, STR_ImagePath, string.Empty, Path.Combine(fullPath, @"m42-800x600.jpg"));
            this.applyNoise = Convert.ToBoolean(profile.GetValue(s_csDriverID, STR_ApplyNoise, string.Empty, "false"), CultureInfo.InvariantCulture);

            // default to min = max && gains = null - no gain control
            this.gainMin = Convert.ToInt16(profile.GetValue(s_csDriverID, "GainMin", string.Empty, "0"), CultureInfo.InvariantCulture);
            this.gainMax = Convert.ToInt16(profile.GetValue(s_csDriverID, "GainMax", string.Empty, "0"), CultureInfo.InvariantCulture);
            this.gains = null;
            // check the length of the gains string, non zero and we set Gains strings
            if (Convert.ToInt16(profile.GetValue(s_csDriverID, "Gains", string.Empty, "0"), CultureInfo.InvariantCulture) > 0)
            {
                this.gains = new string[] { "ISO 100", "ISO 200", "ISO 400", "ISO 800", "ISO 1600" };
                this.gainMin = (short)this.gains.GetLowerBound(0);
                this.gainMax = (short)this.gains.GetUpperBound(0);
            }
            // and if the Gains parameter is zero we just use the min and max.
        }

        private void SaveToProfile()
        {
            Profile profile = new Profile();
            profile.DeviceType = "Camera";

            profile.WriteValue(s_csDriverID, STR_InterfaceVersion, this.interfaceVersion.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_PixelSizeX, this.pixelSizeX.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_PixelSizeY, this.pixelSizeY.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_FullWellCapacity, this.fullWellCapacity.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_MaxADU, this.maxADU.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_ElectronsPerADU, this.electronsPerADU.ToString(CultureInfo.InvariantCulture));

            profile.WriteValue(s_csDriverID, STR_CameraXSize, this.cameraXSize.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_CameraYSize, this.cameraYSize.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_CanAsymmetricBin, this.canAsymmetricBin.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_MaxBinX, this.maxBinX.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_MaxBinY, this.maxBinY.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_HasShutter, this.hasShutter.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_SensorName, this.sensorName.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_SensorType, ((int)this.sensorType).ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_BayerOffsetX, this.bayerOffsetX.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_BayerOffsetY, this.bayerOffsetY.ToString(CultureInfo.InvariantCulture));

            profile.WriteValue(s_csDriverID, STR_HasCooler, this.hasCooler.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_CanSetCCDTemperature, this.canSetCcdTemperature.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_CanGetCoolerPower, this.canGetCoolerPower.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_SetCCDTemperature, this.setCcdTemperature.ToString(CultureInfo.InvariantCulture));

            profile.WriteValue(s_csDriverID, STR_CanAbortExposure, this.canAbortExposure.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_CanStopExposure, this.canStopExposure.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_MaxExposure, this.exposureMax.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_MinExposure, this.exposureMin.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_ExposureResolution, this.exposureResolution.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, STR_ImagePath, this.imagePath);
            profile.WriteValue(s_csDriverID, STR_ApplyNoise, this.applyNoise.ToString(CultureInfo.InvariantCulture));

            // entertaining setting the gain options.
            profile.WriteValue(s_csDriverID, "GainMin", this.gainMin.ToString(CultureInfo.InvariantCulture));
            profile.WriteValue(s_csDriverID, "GainMax", this.gainMax.ToString(CultureInfo.InvariantCulture));

            if (this.gains != null && this.gains.Length > 0)
            {
                // gain control using Gains
                profile.WriteValue(s_csDriverID, "Gains", this.gains.Length.ToString(CultureInfo.InvariantCulture));
                profile.WriteValue(s_csDriverID, "GainMin", this.gains.GetLowerBound(0).ToString(CultureInfo.InvariantCulture));
                profile.WriteValue(s_csDriverID, "GainMax", this.gains.GetUpperBound(0).ToString(CultureInfo.InvariantCulture));
            }
            else if (this.gainMax > this.gainMin)
            {
                // gain control usingh min and max
                profile.WriteValue(s_csDriverID, "Gains", "0");
                profile.WriteValue(s_csDriverID, "GainMin", this.gainMin.ToString(CultureInfo.InvariantCulture));
                profile.WriteValue(s_csDriverID, "GainMax", this.gainMax.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                // no gain control
                profile.WriteValue(s_csDriverID, "Gains", "0");
                profile.WriteValue(s_csDriverID, "GainMin", "0");
                profile.WriteValue(s_csDriverID, "GainMax", "0");
            }
        }

        private void InitialiseSimulator()
        {
            this.ReadFromProfile();

            this.startX = 0;
            this.startY = 0;
            this.binX = 1;
            this.binY = 1;
            this.numX = this.cameraXSize;
            this.numY = this.cameraYSize;
            
            this.cameraState= CameraStates.cameraIdle;
            this.coolerOn = false;
            this.coolerPower = 0;
            this.heatSinkTemperature = 20;
            this.ccdTemperature = 15;
            this.readoutMode = 0;
            this.fastReadout = false;
            //this.ReadImageFile();
        }

        private delegate int PixelProcess(double value);

        private void FillImageArray()
        {
            PixelProcess pixelProcess = new PixelProcess(NoNoise);
            ShutterProcess shutterProcess = new ShutterProcess(BinData);

            if (this.applyNoise)
                pixelProcess = new PixelProcess(Poisson);
            if (this.HasShutter && this.darkFrame)
                shutterProcess = new ShutterProcess(DarkData);

            double readNoise = 3;
            // dark current 1 ADU/sec at 0C doubling for every 5C increase
            double darkCurrent = Math.Pow(2,this.ccdTemperature/5);
            darkCurrent *= this.lastExposureDuration;
            // add read noise, should be in quadrature
            darkCurrent += readNoise;
            // fill the array using binning and image offsets
            // indexes into the imageData
            for (int y = 0; y < this.numY; y++)
            {
                for (int x = 0; x < this.numX; x++)
                {
                    double s;
                    if (this.sensorType == SensorType.Color)
                    {
                        s = shutterProcess((x + this.startX) * this.binX, (y + this.startY) * this.binY, 0);
                        this.imageArrayColour[x, y, 0] = pixelProcess(s + darkCurrent);
                        s = shutterProcess((x + this.startX) * this.binX, (y + this.startY) * this.binY, 1);
                        this.imageArrayColour[x, y, 1] = pixelProcess(s + darkCurrent);
                        s = shutterProcess((x + this.startX) * this.binX, (y + this.startY) * this.binY, 2);
                        this.imageArrayColour[x, y, 2] = pixelProcess(s + darkCurrent);
                    }
                    else
                    {
                        s = shutterProcess((x + this.startX) * this.binX, (y + this.startY) * this.binY, 0);
                        this.imageArray[x, y] = pixelProcess(s + darkCurrent);
                    }
                    //s *= this.lastExposureDuration;
                }
            }
        }

        private Random R = new Random();

        private int Poisson(double lambda)
        {
            // use normal distribution for large values
            // because Poisson falls over and gets slow
            if (lambda > 50)
                return Math.Min((int)BoxMuller(lambda, Math.Sqrt(lambda)), this.MaxADU);

            double L = Math.Exp(-lambda);
            double p = 1.0;
            int k = 0;
            do
            {
                k++;
                p *= R.NextDouble();
            }
            while (p > L);
            return Math.Min(k-1, this.maxADU);
        }

        /// <summary>
        /// normal random variate generator
        /// </summary>
        /// <param name="m">mean</param>
        /// <param name="s">standard deviation</param>
        /// <returns></returns>
        private double BoxMuller(double m, double s)
        {
	        double x1, x2, w, y1;
	        do 
            {
		        x1 = 2.0 * R.NextDouble() - 1.0;
		        x2 = 2.0 * R.NextDouble() - 1.0;
		        w = x1 * x1 + x2 * x2;
	        } while ( w >= 1.0 );

	        w = Math.Sqrt( (-2.0 * Math.Log( w ) ) / w );
	        y1 = x1 * w;
	        return( m + y1 * s );
        }

        private int NoNoise(double value)
        {
            return Convert.ToInt32(Math.Min(value, this.maxADU));
        }

        /// <summary>
        /// Delegate to handle getting the binned or unbinned data for each pixel
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        private delegate double ShutterProcess(int x, int y, int p);

        /// <summary>
        /// returns the sum of the image data for binX x BinY pixels
        /// </summary>
        /// <param name="x">left of bin area</param>
        /// <param name="y">top of bin area</param>
        /// <returns></returns>
        private double BinData(int x, int y, int p)
        {
            double s = 0;
            for (int k = 0; k < this.binY; k++)
            {
                for (int l = 0; l < this.binX; l++)
                {
                    s += imageData[l + x, k + y, p];
                }
            }
            return s * this.lastExposureDuration;
        }

        private double DarkData(int x, int y, int p)
        {
            return 5.0 * this.binX * this.binY;
        }


        private delegate void GetData(int x, int y);
        private Bitmap bmp;
        // bayer offsets
        private int x0;
        private int x1;
        private int x2;
        private int x3;

        private int y0;
        private int y1;
        private int y2;
        private int y3;

        private int stepX;
        private int stepY;

        /// <summary>
        /// reads image data from a file and puts it into a buffer in a format suitable for
        /// processing into the ImageArray.  The size of the image must be the same as the
        /// full frame image data.
        /// </summary>
        private void ReadImageFile()
        {
            this.imageData = new float[this.cameraXSize, this.cameraYSize, 1];
            try
            {
                bmp = (Bitmap)Image.FromFile(this.imagePath);

                //x0 = bayerOffsetX;
                //x1 = (bayerOffsetX + 1) & 1;
                //y0 = bayerOffsetY;
                //y1 = (bayerOffsetY + 1) & 1;

                GetData getData = new GetData(MonochromeData);
                switch (this.sensorType)
                {
                    case SensorType.Monochrome:
                        stepX = 1;
                        stepY = 1;
                        break;
                    case SensorType.RGGB:
                        getData = new GetData(RGGBData);
                        stepX = 2;
                        stepY = 2;
                        break;
                    case SensorType.CMYG:
                        getData = new GetData(CMYGData);
                        stepX = 2;
                        stepY = 2;
                        break;
                    case SensorType.CMYG2:
                        getData = new GetData(CMYG2Data);
                        stepX = 2;
                        stepY = 4;
                        //y0 = (bayerOffsetY) & 3;
                        //y1 = (bayerOffsetY + 1) & 3;
                        //y2 = (bayerOffsetY + 2) & 3;
                        //y3 = (bayerOffsetY + 3) & 3;
                        break;
                    case SensorType.LRGB:
                        getData = new GetData(LRGBData);
                        stepX = 4;
                        stepY = 4;
                        break;
                    case SensorType.Color:
                        this.imageData = new float[this.cameraXSize, this.cameraYSize, 3];
                        getData = new GetData(ColorData);
                        stepX = 1;
                        stepY = 1;
                        break;
                    default:
                        break;
                }
                x0 = bayerOffsetX;
                x1 = (x0 + 1) & (stepX - 1);
                x2 = (x0 + 2) & (stepX - 1);
                x3 = (x0 + 3) & (stepX - 1);
                y0 = bayerOffsetY;
                y1 = (y0 + 1) & (stepY - 1);
                y2 = (y0 + 2) & (stepY - 1);
                y3 = (y0 + 3) & (stepY - 1);

                int w = Math.Min(this.cameraXSize, bmp.Width*stepX);
                int h = Math.Min(this.cameraYSize, bmp.Height * stepY);
                for (int y = 0; y < h; y+=stepY)
                {
                    for (int x = 0; x < w; x+=stepX)
                    {
                        getData(x, y);
                    }
                }

            }
            catch
            {
            }
        }

        // get data using the sensor types
        private void MonochromeData(int x, int y)
        {
            imageData[x, y, 0] = (bmp.GetPixel(x, y).GetBrightness() * 255);
        }
        private void RGGBData(int x, int y)
        {
            Color px = bmp.GetPixel(x/2, y/2);
            imageData[x + x0, y + y0, 0] = px.R;      // red
            imageData[x + x1, y + y0, 0] = px.G;      // green
            imageData[x + x0, y + y1, 0] = px.G;      // green
            imageData[x + x1, y + y1, 0] = px.B;      // blue
        }
        private void CMYGData(int x, int y)
        {
            Color px = bmp.GetPixel(x/2, y/2);
            imageData[x + x0, y + y0, 0] = (px.R + px.G) / 2;       // yellow
            imageData[x + x1, y + y0, 0] = (px.G + px.B) / 2;       // cyan
            imageData[x + x0, y + y1, 0] = px.G;                    // green
            imageData[x + x1, y + y1, 0] = (px.R + px.B) / 2;       // magenta
        }
        private void CMYG2Data(int x, int y)
        {
            Color px = bmp.GetPixel(x/2, y/2);
            imageData[x + x0, y + y0, 0] = (px.G);
            imageData[x + x1, y + y0, 0] = (px.B + px.R) / 2;      // magenta
            imageData[x + x0, y + y1, 0] = (px.G + px.B) / 2;      // cyan
            imageData[x + x1, y + y1, 0] = (px.R + px.G) / 2;      // yellow
            px = bmp.GetPixel(x / 2, (y/2) + 1);
            imageData[x + x0, y + y2, 0] = (px.B + px.R) / 2;      // magenta
            imageData[x + x1, y + y2, 0] = (px.G);
            imageData[x + x0, y + y3, 0] = (px.G + px.B) / 2;      // cyan
            imageData[x + x1, y + y3, 0] = (px.R + px.G) / 2;      // yellow
        }
        private void LRGBData(int x, int y)
        {
            Color px = bmp.GetPixel(x/2, y/2);
            imageData[x + x0, y + y0, 0] = px.GetBrightness() * 255;
            imageData[x + x1, y + y0, 0] = (px.R);
            imageData[x + x0, y + y1, 0] = (px.R);
            imageData[x + x1, y + y1, 0] = px.GetBrightness() * 255;
            px = bmp.GetPixel((x/2)+ 1, y/2);
            imageData[x + x2, y + y0, 0] = px.GetBrightness() * 255;
            imageData[x + x3, y + y0, 0] = (px.G);
            imageData[x + x2, y + y1, 0] = (px.G);
            imageData[x + x3, y + y1, 0] = px.GetBrightness() * 255;
            px = bmp.GetPixel(x/2, (y/2)+1);
            imageData[x + x0, y + y2, 0] = px.GetBrightness() * 255;
            imageData[x + x1, y + y2, 0] = (px.G);
            imageData[x + x0, y + y3, 0] = (px.G);
            imageData[x + x1, y + y3, 0] = px.GetBrightness() * 255;
            px = bmp.GetPixel((x/2)+1, (y/2)+1);
            imageData[x + x2, y + y2, 0] = px.GetBrightness() * 255;
            imageData[x + x3, y + y2, 0] = (px.B);
            imageData[x + x2, y + y3, 0] = (px.B);
            imageData[x + x3, y + y3, 0] = px.GetBrightness() * 255;
        }
        private void ColorData(int x, int y)
        {
            imageData[x, y, 0] = (bmp.GetPixel(x, y).R);
            imageData[x, y, 1] = (bmp.GetPixel(x, y).G);
            imageData[x, y, 2] = (bmp.GetPixel(x, y).B);
        }
        #endregion

    }
}

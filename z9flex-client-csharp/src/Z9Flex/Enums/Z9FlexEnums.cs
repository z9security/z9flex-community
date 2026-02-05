
/*
 * Copyright (C) 2011-Infinity Z9 Security. All rights reserved.
 */

// Z9/FL=X Community Profile API enumeration constants.
namespace Z9Flex.Enums
{

	/** The type of client connection, when authenticating through an API. */
	public static class ApiClientType
	{
		/** Web application. */
		public const int WebApp = 0;
		/** Non-application API purposes, e.g. machine-to-machine interfaces, utilities. */
		public const int Api = 2;
	}

	/** Binary element type. */
	public static class BinaryElementType
	{
		/** Static (fixed) element - StaticBinaryElement. */
		public const int Static = 0;
		/** Parity - ParityBinaryElement. */
		public const int Parity = 1;
		/** Data field - FieldBinaryElement. */
		public const int Field = 2;
	}

	/** Credential component presence. */
	public static class CredComponentPresence
	{
		/** Absent. */
		public const int Absent = 0;
		/** Required. */
		public const int Required = 1;
		/** Optional. */
		public const int Optional = 2;
	}

	/** Credential reader communication type. */
	public static class CredReaderCommType
	{
		/** OSDP, half-duplex. This is "normal" OSDP. */
		public const int OsdpHalfDuplex = 6;
	}

	/** Credential reader LED type. */
	public static class CredReaderLedType
	{
		/** OSDP - this is more or less implied by CredReaderCommType.OSDP. */
		public const int Osdp = 2;
	}

	/** Credential reader tamper type. */
	public static class CredReaderTamperType
	{
		/** OSDP - this is more or less implied by CredReaderCommType.OSDP. */
		public const int Osdp = 2;
	}

	/** Data format field type. */
	public static class DataFormatField
	{
		/** Credential number (Card number). */
		public const int CredNum = 0;
		/** Facility code. */
		public const int FacilityCode = 1;
	}

	/** Data format type. */
	public static class DataFormatType
	{
		/** Binary format, used for Wiegand, etc. */
		public const int Binary = 1;
	}

	/** Data layout type. */
	public static class DataLayoutType
	{
		/** Basic - only has one data format (DataFormat). */
		public const int Basic = 0;
	}

	/** Device action params type. */
	public static class DevActionParamsType
	{
		/** DoorModeDevActionParams, for DevActionType.DOOR_MODE_CHANGE. */
		public const int DoorMode = 1;
		/** DoorMomentaryUnlockDevActionParams, for DevActionType.DOOR_MOMENTARY_UNLOCK. */
		public const int DoorMomentaryUnlock = 10;
	}

	/** Template types for creating new device data. */
	public static class DevCreationTemplateType
	{
		/** Create - Controller - Community. */
		public const int IoControllerCommunity = 250;
	}

	/** Device model. */
	public static class DevMod
	{
		/** IO_CONTROLLER model - z9io - Custom. */
		public const int IoControllerZ9Custom = 0;
		/** SENSOR model - digital. */
		public const int SensorDigital = 14;
		/** ACTUATOR model - digital. */
		public const int ActuatorDigital = 18;
		/** IO_CONTROLLER - Z9-Security SP-Core. */
		public const int IoControllerZ9Spcore = 26;
		/** Externally-defined - IO_CONTROLLER. */
		public const int IoControllerExternal = 46;
		/** IO_CONTROLLER model - Community. */
		public const int IoControllerCommunity = 164;
		/** IO_CONTROLLER model - Community sub-controller. */
		public const int IoControllerCommunitySub = 165;
		/** CRED_READER model - OSDP (generic). */
		public const int CredReaderOsdp = 227;
		/** DOOR model - Community. */
		public const int DoorCommunity = 259;
	}

	/** Device model configuration type. */
	public static class DevModConfigType
	{
		/** SpCoreControllerDevModConfig. */
		public const int ControllerZ9Spcore = 13;
	}

	/** Device platform. */
	public static class DevPlatform
	{
		/** Z9 Security, including Z9/Open and Z9/Flex. */
		public const int Z9Security = 0;
		/** External. */
		public const int External = 9;
		/** PKOC. */
		public const int Pkoc = 11;
		/** Community. */
		public const int Community = 17;
		/** PSIA. */
		public const int Psia = 19;
	}

	/** Device sub-type. */
	public static class DevSubType
	{
		/** IO_CONTROLLER - sub-controller. */
		public const int IoControllerSub = 7;
	}

	/** Device type. */
	public static class DevType
	{
		/** Device representing a node, which is a server or service running the Z9/FLEX software. */
		public const int NodeDev = 0;
		/** A controller, such as an access control panel. It may be an intelligent controller, a downstream controller, or a virtual/placeholder controller. */
		public const int IoController = 1;
		/** Sensor (input), including door contact and REX inputs, AUX inputs, alarm inputs, environmental inputs, others. */
		public const int Sensor = 2;
		/** Actuator (output), including relay outputs, AUX outputs, LED outputs, others. */
		public const int Actuator = 3;
		/** Credential reader. */
		public const int CredReader = 4;
		/** Access-control door (access point). */
		public const int Door = 5;
	}

	/** Device use - semantic role of a device within the system. */
	public static class DevUse
	{
		/** ACTUATOR - door strike. HAS SEMANTICS. */
		public const int ActuatorDoorStrike = 0;
		/** ACTUATOR - Cred reader LED - Red. HAS SEMANTICS. */
		public const int ActuatorCredReaderLedRed = 2;
		/** ACTUATOR - Cred reader LED - Green. HAS SEMANTICS. */
		public const int ActuatorCredReaderLedGreen = 3;
		/** ACTUATOR - Cred reader beeper. HAS SEMANTICS. */
		public const int ActuatorCredReaderBeeper = 4;
		/** SENSOR - door contact. HAS SEMANTICS. */
		public const int SensorDoorContact = 10;
		/** SENSOR - REX (request to exit). HAS SEMANTICS. */
		public const int SensorRex = 11;
		/** SENSOR - Tamper. HAS SEMANTICS. */
		public const int SensorTamper = 12;
		/** SENSOR - Power monitor. HAS SEMANTICS. */
		public const int SensorPower = 13;
		/** SENSOR - Battery monitor. HAS SEMANTICS. */
		public const int SensorBattery = 14;
		/** CRED_READER - for a door. HAS SEMANTICS. */
		public const int CredReaderDoor = 38;
		/** DOOR - exit (as opposed to entry). HAS SEMANTICS. Logical parent must be a DOOR. */
		public const int DoorExit = 39;
		/** IO_CONTROLLER - secondary. HAS SEMANTICS. */
		public const int IoControllerSecondary = 44;
	}

	/** Door mode static state. */
	public static class DoorModeStaticState
	{
		/** Always locked, no access. */
		public const int Locked = 0;
		/** Always unlocked. */
		public const int Unlocked = 1;
		/** Normally locked (secure) but available for access-controlled entry. */
		public const int AccessControlled = 2;
	}

	/** Encryption key type. */
	public static class EncryptionKeyType
	{
		/** The public key of a public/private key pair. */
		public const int Public = 0;
		/** The private key of a public/private key pair. */
		public const int Private = 1;
		/** A secret key (symmetric key encryption). */
		public const int Secret = 2;
	}

	/** Event code. */
	public static class EvtCode
	{
		/** Z9/FLEX startup. */
		public const int DaemonStartup = 0;
		/** Login granted (application access). */
		public const int LoginGranted = 6;
		/** Login denied (application access). */
		public const int LoginDenied = 7;
		/** Logout (application access). */
		public const int Logout = 8;
		/** Controller startup. */
		public const int ControllerStartup = 9;
		/** Controller online. */
		public const int ControllerOnline = 10;
		/** Controller offline. */
		public const int ControllerOffline = 11;
		/** Credential reader online. */
		public const int CredReaderOnline = 15;
		/** Credential reader offline. */
		public const int CredReaderOffline = 16;
		/** Door access granted. */
		public const int DoorAccessGranted = 48;
		/** Door access denied. */
		public const int DoorAccessDenied = 49;
		/** Door forced open. */
		public const int DoorForced = 52;
		/** Door not forced open. */
		public const int DoorNotForced = 53;
		/** Door held open. */
		public const int DoorHeld = 54;
		/** Door not held open. */
		public const int DoorNotHeld = 55;
		/** Door opened. */
		public const int DoorOpened = 56;
		/** Door closed. */
		public const int DoorClosed = 57;
		/** Door locked. */
		public const int DoorLocked = 58;
		/** Door unlocked. */
		public const int DoorUnlocked = 59;
		/** Door mode - Unlocked - free access. */
		public const int DoorModeStaticStateUnlocked = 60;
		/** Door mode - No access. */
		public const int DoorModeStaticStateLocked = 61;
		/** Door mode - Card only. */
		public const int DoorModeCardOnly = 62;
		/** Door mode - Card and PIN. */
		public const int DoorModeCardAndConfirmingPin = 63;
		/** Door mode - PIN only. */
		public const int DoorModeUniquePinOnly = 64;
		/** Door mode - Card or PIN. */
		public const int DoorModeCardOnlyOrUniquePin = 65;
		/** Exit requested. */
		public const int ExitRequested = 86;
		/** Momentary unlock. */
		public const int MomentaryUnlock = 87;
		/** On primary power. */
		public const int PowerPrimary = 113;
		/** Off primary power. */
		public const int PowerOffPrimary = 114;
		/** No power. */
		public const int PowerNone = 115;
		/** Battery OK. */
		public const int BatteryOk = 116;
		/** Battery low. */
		public const int BatteryLow = 117;
		/** Battery fail. */
		public const int BatteryFail = 118;
		/** Tamper normal (no tamper). */
		public const int TamperNormal = 125;
		/** Tamper. */
		public const int Tamper = 126;
		/** Schedule active. */
		public const int SchedActive = 127;
		/** Schedule inactive. */
		public const int SchedInactive = 128;
		/** Externally defined. */
		public const int External = 153;
		/** Battery critical. */
		public const int BatteryCritical = 244;
		/** The CredReader has had its power cycled - for example an OSDP reader reporting the power status flag as active. */
		public const int CredReaderPowerCycle = 259;
	}

	/** Event sub-code. */
	public static class EvtSubCode
	{
		/** Access denied - inactive. */
		public const int AccessDeniedInactive = 0;
		/** Access denied - not yet effective. */
		public const int AccessDeniedNotEffective = 6;
		/** Access denied - expired. */
		public const int AccessDeniedExpired = 7;
		/** Access denied - no privileges. */
		public const int AccessDeniedNoPriv = 10;
		/** Access denied - outside schedule. */
		public const int AccessDeniedOutsideSched = 11;
		/** Access denied - unknown credential (card) number. */
		public const int AccessDeniedUnknownCredNum = 14;
		/** Access denied - unknown credential (card) number format. */
		public const int AccessDeniedUnknownCredNumFormat = 15;
		/** Access denied - unknown unique PIN. */
		public const int AccessDeniedUnknownCredUniquePin = 17;
		/** Access denied - incorrect facility code. */
		public const int AccessDeniedIncorrectFacilityCode = 20;
		/** Access denied - door mode - static locked (no access). */
		public const int AccessDeniedDoorModeStaticLocked = 22;
		/** Access denied - door mode - doesn't allow card. */
		public const int AccessDeniedDoorModeDoesntAllowCard = 23;
		/** Access denied - door mode - doesn't allow unique PIN. */
		public const int AccessDeniedDoorModeDoesntAllowUniquePin = 24;
		/** Access denied - no confirming PIN for credential. */
		public const int AccessDeniedNoConfirmingPinForCred = 25;
		/** Access denied - incorrect confirming PIN. */
		public const int AccessDeniedIncorrectConfirmingPin = 27;
		/** Externally defined. */
		public const int External = 104;
	}

	/** Privilege type. */
	public static class PrivType
	{
		/** Door access privilege. */
		public const int Door = 0;
	}

	/** Schedule day. */
	public static class SchedDay
	{
		/** Monday. */
		public const int Mon = 0;
		/** Tuesday. */
		public const int Tues = 1;
		/** Wednesday. */
		public const int Wed = 2;
		/** Thursday. */
		public const int Thur = 3;
		/** Friday. */
		public const int Fri = 4;
		/** Saturday. */
		public const int Sat = 5;
		/** Sunday. */
		public const int Sun = 6;
	}
}

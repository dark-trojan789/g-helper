﻿namespace GHelper.Peripherals.Mouse.Models
{
    //P504
    public class GladiusIIOrigin : AsusMouse
    {
        public GladiusIIOrigin() : base(0x0B05, 0x1877, "mi_02", false)
        {
        }

        public GladiusIIOrigin(ushort productId, string path) : base(0x0B05, productId, path, false)
        {
        }

        public override int DPIProfileCount()
        {
            return 2;
        }

        public override string GetDisplayName()
        {
            return "Gladius II Origin";
        }


        public override PollingRate[] SupportedPollingrates()
        {
            return new PollingRate[] {
                PollingRate.PR125Hz,
                PollingRate.PR250Hz,
                PollingRate.PR500Hz,
                PollingRate.PR1000Hz
            };
        }

        public override int ProfileCount()
        {
            return 1;
        }
        public override int MaxDPI()
        {
            return 12_000;
        }

        public override bool HasRGB()
        {
            return true;
        }

        public override bool HasAutoPowerOff()
        {
            return false;
        }

        public override bool HasAngleSnapping()
        {
            return true;
        }

        public override bool HasAngleTuning()
        {
            return false;
        }

        public override bool HasDebounceSetting()
        {
            return true;
        }
        public override bool HasLiftOffSetting()
        {
            return true;
        }

        public override bool HasLowBatteryWarning()
        {
            return false;
        }

        public override bool HasBattery()
        {
            return false;
        }

        public override bool HasDPIColors()
        {
            return false;
        }

        public override bool IsLightingModeSupported(LightingMode lightingMode)
        {
            return lightingMode == LightingMode.Static
                || lightingMode == LightingMode.Breathing
                || lightingMode == LightingMode.ColorCycle
                || lightingMode == LightingMode.Rainbow
                || lightingMode == LightingMode.React
                || lightingMode == LightingMode.Comet;
        }

        public override LightingZone[] SupportedLightingZones()
        {
            return new LightingZone[] { LightingZone.Logo, LightingZone.Scrollwheel, LightingZone.Underglow };
        }

        public override int DPIIncrements()
        {
            return 100;
        }

        public override bool CanChangeDPIProfile()
        {
            return true;
        }

        public override int MaxBrightness()
        {
            return 4;
        }

        protected override byte[] GetUpdateLightingModePacket(LightingSetting lightingSetting, LightingZone zone)
        {
            /*
             * This mouse uses different speed values for rainbow mode compared to others.
             * 51 28 03 00 03 04 FF 00 00 00 00 [8C] 00 00 00 00
             * 51 28 03 00 03 04 FF 00 00 00 00 [64] 00 00 00 00
             * 51 28 03 00 03 04 FF 00 00 00 00 [3F] 00 00 00 00
             */

            if (lightingSetting.LightingMode == LightingMode.Rainbow)
            {
                byte speed = 0x3F;

                switch (lightingSetting.AnimationSpeed)
                {
                    case AnimationSpeed.Slow:
                        speed = 0x3F;
                        break;
                    case AnimationSpeed.Medium:
                        speed = 0x64;
                        break;
                    case AnimationSpeed.Fast:
                        speed = 0x8C;
                        break;
                }

                return new byte[] { reportId, 0x51, 0x28, (byte)zone, 0x00,
                    IndexForLightingMode(lightingSetting.LightingMode),
                    (byte)lightingSetting.Brightness,
                    0xFF, 0x00, 0x00,
                    (byte)(SupportsAnimationDirection(lightingSetting.LightingMode) ? lightingSetting.AnimationDirection : 0x00),
                    (byte)((lightingSetting.RandomColor && SupportsRandomColor(lightingSetting.LightingMode)) ? 0x01: 0x00),
                    (byte)(SupportsAnimationSpeed(lightingSetting.LightingMode) ? speed : 0x00)
                };
            }

            return base.GetUpdateLightingModePacket(lightingSetting, zone);
        }

        protected override byte[] GetReadLightingModePacket(LightingZone zone)
        {
            return new byte[] { 0x00, 0x12, 0x03, 0x00 };
        }

        protected LightingSetting? ParseLightingSetting(byte[] packet, LightingZone zone)
        {
            if (packet[1] != 0x12 || packet[2] != 0x03)
            {
                return null;
            }

            int offset = 5 + (((int)zone) * 5);

            LightingSetting setting = new LightingSetting();

            setting.LightingMode = LightingModeForIndex(packet[offset + 0]);
            setting.Brightness = packet[offset + 1];

            setting.RGBColor = Color.FromArgb(packet[offset + 2], packet[offset + 3], packet[offset + 4]);


            return setting;
        }

        public override void ReadLightingSetting()
        {
            if (!HasRGB())
            {
                return;
            }
            //Mouse sends all lighting zones in one response                       Direction, Random col, Speed
            //00 12 03 00 00 [00 04 ff 00 80] [00 04 00 ff ff] [00 04 ff ff ff] 00 [00] [00] [00] 00 00 
            //00 12 03 00 00 [03 04 00 00 00] [03 04 00 00 00] [03 04 00 00 00] 00 [00] [00] [07] 00 00
            byte[]? response = WriteForResponse(GetReadLightingModePacket(LightingZone.All));
            if (response is null) return;

            LightingZone[] lz = SupportedLightingZones();
            for (int i = 0; i < lz.Length; ++i)
            {
                LightingSetting? ls = ParseLightingSetting(response, lz[i]);
                if (ls is null)
                {
                    Logger.WriteLine(GetDisplayName() + ": Failed to read RGB Setting for Zone " + lz[i].ToString());
                    continue;
                }
                ls.AnimationDirection = SupportsAnimationDirection(ls.LightingMode)
                   ? (AnimationDirection)response[21]
                   : AnimationDirection.Clockwise;

                ls.RandomColor = SupportsRandomColor(ls.LightingMode) && response[22] == 0x01;

                ls.AnimationSpeed = SupportsAnimationSpeed(ls.LightingMode)
                    ? (AnimationSpeed)response[23]
                    : AnimationSpeed.Medium;

                if (ls.AnimationSpeed != AnimationSpeed.Fast
                    && ls.AnimationSpeed != AnimationSpeed.Medium
                    && ls.AnimationSpeed != AnimationSpeed.Slow)
                {
                    ls.AnimationSpeed = AnimationSpeed.Medium;
                }

                Logger.WriteLine(GetDisplayName() + ": Read RGB Setting for Zone " + lz[i].ToString() + ": " + ls.ToString());
                LightingSetting[i] = ls;
            }
        }
    }

    //P502
    public class GladiusII : GladiusIIOrigin
    {
        public GladiusII() : base(0x1845, "mi_02")
        {

        }
        public override string GetDisplayName()
        {
            return "Gladius II Origin";
        }

        public override int ProfileCount()
        {
            return 3;
        }
    }
}

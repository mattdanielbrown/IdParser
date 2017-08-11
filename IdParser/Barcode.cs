﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using IdParser.Attributes;

namespace IdParser
{
    public static class Barcode
    {
        private const char ExpectedComplianceIndicator = (char) 64;
        private const char ExpectedDataElementSeparator = (char) 10;
        private const char ExpectedRecordSeparator = (char) 30;
        private const char ExpectedSegmentTerminator = (char) 13;
        private const string ExpectedFileType = "ANSI ";

        /// <summary>
        /// Parses the raw input from the PDF417 barcode into an IdentificationCard or DriversLicense object.
        /// </summary>
        /// <param name="rawPdf417Input">The string to parse the information out of</param>
        /// <param name="validationLevel">
        /// Specifies the level of <see cref="Validation"/> that will be performed.
        /// Strict validation will ensure the input fully conforms to the AAMVA standard.
        /// No validation will be performed if none is specified and exceptions will not be thrown
        /// for elements that do not match or do not adversely affect parsing.
        /// </param>
        public static IdentificationCard Parse(string rawPdf417Input, Validation validationLevel = Validation.Strict)
        {
            if (validationLevel == Validation.Strict)
            {
                ValidateFormat(rawPdf417Input);
            }
            else
            {
                rawPdf417Input = FixIncorrectHeader(rawPdf417Input);
                rawPdf417Input = RemoveIncorrectCarriageReturns(rawPdf417Input);
            }

            var version = ParseAamvaVersion(rawPdf417Input);
            var subfileRecords = GetSubfileRecords(version, rawPdf417Input);
            var country = ParseCountry(version, subfileRecords);
            
            if (ParseSubfileType(version, rawPdf417Input) == "DL")
            {
                return new DriversLicense(version, country, rawPdf417Input, subfileRecords);
            }

            return new IdentificationCard(version, country, rawPdf417Input, subfileRecords);
        }

        /// <summary>
        /// Gets the AAMVA version of the input.
        /// </summary>
        /// <param name="input">The raw PDF417 barcode data</param>
        public static Version ParseAamvaVersion(string input)
        {
            if (input == null || input.Length < 17)
            {
                throw new ArgumentException("Input must not be null or less than 17 characters in order to parse the version.", nameof(input));
            }

            var version = ParseAamvaVersionNumber(input);

            if (Enum.IsDefined(typeof(Version), version))
            {
                return (Version)version;
            }

            return Version.Future;
        }

        /// <summary>
        /// Gets the value of the <see cref="DescriptionAttribute"/> on the <see cref="Enum"/>.
        /// </summary>
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetTypeInfo().GetField(value.ToString());
            var attribute = field.GetCustomAttribute<DescriptionAttribute>();

            return attribute == null ? value.ToString() : attribute.Description;
        }

        /// <summary>
        /// Gets the value of the <see cref="AbbreviationAttribute"/> on the <see cref="Enum"/>.
        /// </summary>
        public static string GetAbbreviation(this Enum value)
        {
            var field = value.GetType().GetTypeInfo().GetField(value.ToString());
            var attribute = field.GetCustomAttribute<AbbreviationAttribute>();

            return attribute == null ? value.ToString() : attribute.Abbreviation;
        }

        /// <summary>
        /// Gets the value of the <see cref="CountryAttribute"/> on the <see cref="Enum"/>.
        /// </summary>
        public static Country GetCountry(this Enum value)
        {
            var field = value.GetType().GetTypeInfo().GetField(value.ToString());
            var attribute = field.GetCustomAttribute<CountryAttribute>();

            return attribute?.Country ?? Country.Unknown;
        }

        private static void ValidateFormat(string input)
        {
            if (input.Length < 31)
            {
                throw new ArgumentException($"The input is missing required header elements and is not a valid AAMVA format. Expected at least 31 characters. Received {input.Length}.", nameof(input));
            }

            var complianceIndicator = ParseComplianceIndicator(input);
            if (complianceIndicator != ExpectedComplianceIndicator)
            {
                throw new ArgumentException($"The compliance indicator is invalid. Expected '{ExpectedComplianceIndicator.ConvertToHex()}'. Received '{complianceIndicator.ConvertToHex()}'.", nameof(input));
            }

            var dataElementSeparator = ParseDataElementSeparator(input);
            if (dataElementSeparator != ExpectedDataElementSeparator)
            {
                throw new ArgumentException($"The data element separator is invalid. Expected '{ExpectedDataElementSeparator.ConvertToHex()}'. Received '{dataElementSeparator.ConvertToHex()}'.", nameof(input));
            }

            var recordSeparator = ParseRecordSeparator(input);
            if (recordSeparator != ExpectedRecordSeparator)
            {
                throw new ArgumentException($"The record separator is invalid. Expected '{ExpectedRecordSeparator.ConvertToHex()}'. Received '{recordSeparator.ConvertToHex()}'.", nameof(input));
            }

            var segmentTerminator = ParseSegmentTerminator(input);
            if (segmentTerminator != ExpectedSegmentTerminator)
            {
                throw new ArgumentException($"The segment terminator is invalid. Expected '{ExpectedSegmentTerminator.ConvertToHex()}'. Received '{segmentTerminator.ConvertToHex()}'.", nameof(input));
            }

            var fileType = ParseFileType(input);
            if (fileType != ExpectedFileType)
            {
                throw new ArgumentException($"The file type is invalid. Expected '{ExpectedFileType}'. Received '{fileType.ConvertToHex()}'.", nameof(input));
            }
        }

        /// <summary>
        /// HID keyboard emulation (and some other methods) tend to replace the \r with \r\n
        /// which is invalid and doesn't conform to the AAMVA standard. This fixes it before attempting to parse the fields.
        /// </summary>
        private static string RemoveIncorrectCarriageReturns(string input)
        {
            var crLf = ExpectedSegmentTerminator.ToString() + ExpectedDataElementSeparator;
            var doesInputContainCrLf = input.IndexOf(crLf, StringComparison.Ordinal) >= 0;

            if (doesInputContainCrLf)
            {
                var replacedString = input.Replace(ExpectedSegmentTerminator.ToString(), string.Empty);

                return replacedString.Substring(0, 3) + ExpectedSegmentTerminator + replacedString.Substring(4);
            }

            return input;
        }

        /// <summary>
        /// HID keyboard emulation, especially entered via a web browser, tends to mutilate the header.
        /// As long as part of the header is correct, this will fix the rest of it to make it parse-able.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string FixIncorrectHeader(string input)
        {
            if (input[0] == '@' &&
                input[1] == ExpectedSegmentTerminator &&
                input[2] == ExpectedDataElementSeparator &&
                input[3] == ExpectedRecordSeparator &&
                input[4] == 'A')
            {
                return input.Insert(4, ExpectedSegmentTerminator.ToString() + ExpectedDataElementSeparator);
            }

            return input;
        }

        private static char ParseComplianceIndicator(string input)
        {
            return input.Substring(0, 1)[0];
        }

        private static string ParseFileType(string input)
        {
            return input.Substring(4, 5);
        }

        private static byte ParseAamvaVersionNumber(string input)
        {
            return Convert.ToByte(input.Substring(15, 2));
        }

        private static char ParseDataElementSeparator(string input)
        {
            return input.Substring(1, 1)[0];
        }

        private static char ParseRecordSeparator(string input)
        {
            return input.Substring(2, 1)[0];
        }

        private static char ParseSegmentTerminator(string input)
        {
            return input.Substring(3, 1)[0];
        }

        private static string ParseSubfileType(Version version, string input)
        {
            if (version == Version.Aamva2000)
            {
                return input.Substring(19, 2);
            }

            return input.Substring(21, 2);
        }

        /// <summary>
        /// Parses the country based on the DCG subfile record. The <see cref="IdentificationCard"/>
        /// constructor attempts to determine the correct country based on the IIN if the country is unknown.
        /// </summary>
        private static Country ParseCountry(Version version, List<string> subfileRecords)
        {
            // Country is not a subfile record in the AAMVA 2000 standard
            if (version == Version.Aamva2000)
            {
                return Country.Unknown;
            }

            foreach (var subfileRecord in subfileRecords)
            {
                var elementId = subfileRecord.Substring(0, 3);
                var data = subfileRecord.Substring(3).Trim();

                if (elementId == "DCG")
                {
                    if (data == "USA")
                    {
                        return Country.USA;
                    }
                    if (data == "CAN" || data == "CDN")
                    {
                        return Country.Canada;
                    }
                }
            }

            return Country.Unknown;
        }

        private static List<string> GetSubfileRecords(Version version, string input)
        {
            int offset = 0;

            if (version == Version.Aamva2000)
            {
                offset = Convert.ToInt32(input.Substring(21, 4));
            }
            else if (version >= Version.Aamva2003)
            {
                offset = Convert.ToInt32(input.Substring(23, 4));
            }

            var records = input.Substring(offset).Split(new[] { ParseDataElementSeparator(input), ParseSegmentTerminator(input) }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var firstRecord = records[0].Substring(0, 2);

            if (firstRecord == "DL" || firstRecord == "ID")
            {
                records[0] = records[0].Substring(2);
            }

            return records;
        }
        
        private static string ConvertToHex(this string value)
        {
            var hex = BitConverter.ToString(Encoding.UTF8.GetBytes(value));

            hex = "0x" + hex.Replace("-", "");

            return hex;
        }

        private static string ConvertToHex(this char value)
        {
            return "0x" + BitConverter.ToString(Encoding.UTF8.GetBytes(new[] { value }));
        }
    }
}

﻿using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IdParser.Tests {
    [TestClass]
    public class DriversLicenseTests {
        [TestMethod]
        public void TestMALicense() {
            var file = File.ReadAllText("MA License.txt");
            var license = IdParser.Parse(file, true);

            Assert.AreEqual("SMITH", license.LastName);
        }

        [TestMethod]
        public void TestNYLicense() {
            var file = File.ReadAllText("NY License.txt");
            var license = IdParser.Parse(file);

            Assert.AreEqual("Michael", license.LastName);
            Assert.AreEqual(new DateTime(2013, 08, 31), license.DateOfBirth);
            Assert.AreEqual("New York", license.IssuerIdentificationNumber.GetDescription());
        }

        [TestMethod]
        public void TestVALicense() {
            var file = File.ReadAllText("VA License.txt");
            var idCard = IdParser.Parse(file);

            Assert.AreEqual("STAUNTON", idCard.City);

            if (idCard is DriversLicense) {
                var license = (DriversLicense)idCard;

                Assert.AreEqual("158X9", license.Jurisdiction.RestrictionCodes);
            }
        }

        [TestMethod]
        public void TestGALicense()
        {
            var file = File.ReadAllText("GA License.txt");
            var idCard = IdParser.Parse(file);

            Assert.AreEqual("123 NORTH STATE ST.", idCard.StreetLine1);
            Assert.AreEqual("Georgia", idCard.IssuerIdentificationNumber.GetDescription());

            if (idCard is DriversLicense) {
                var license = (DriversLicense)idCard;

                Assert.AreEqual("C", license.Jurisdiction.VehicleClass);
            }
        }
    }
}
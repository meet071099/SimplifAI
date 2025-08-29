using Azure.AI.DocumentIntelligence;
using Azure;
using System.Text.Json;

namespace DocumentVerificationAPI.Tests.Mocks
{
    public static class AzureDocumentIntelligenceMocks
    {
        public static class PassportMocks
        {
            public static AnalyzeResult CreateHighConfidencePassportResult()
            {
                var json = @"{
                    ""apiVersion"": ""2023-07-31"",
                    ""modelId"": ""prebuilt-document"",
                    ""stringIndexType"": ""textElements"",
                    ""content"": ""PASSPORT\nUnited States of America\nJOHN DOE\nPassport No: 123456789\nDate of Birth: 01/01/1990"",
                    ""pages"": [
                        {
                            ""pageNumber"": 1,
                            ""angle"": 0,
                            ""width"": 8.5,
                            ""height"": 11,
                            ""unit"": ""inch"",
                            ""lines"": [
                                {
                                    ""content"": ""PASSPORT"",
                                    ""boundingBox"": [1.0, 1.0, 3.0, 1.0, 3.0, 1.5, 1.0, 1.5],
                                    ""spans"": [{ ""offset"": 0, ""length"": 8 }]
                                },
                                {
                                    ""content"": ""JOHN DOE"",
                                    ""boundingBox"": [1.0, 2.0, 3.0, 2.0, 3.0, 2.5, 1.0, 2.5],
                                    ""spans"": [{ ""offset"": 30, ""length"": 8 }]
                                }
                            ]
                        }
                    ],
                    ""documents"": [
                        {
                            ""docType"": ""document"",
                            ""boundingRegions"": [
                                {
                                    ""pageNumber"": 1,
                                    ""boundingBox"": [0, 0, 8.5, 0, 8.5, 11, 0, 11]
                                }
                            ],
                            ""fields"": {
                                ""DocumentType"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""passport"",
                                    ""content"": ""passport"",
                                    ""boundingBox"": [1.0, 1.0, 3.0, 1.5],
                                    ""confidence"": 0.95,
                                    ""spans"": [{ ""offset"": 0, ""length"": 8 }]
                                },
                                ""FirstName"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""JOHN"",
                                    ""content"": ""JOHN"",
                                    ""boundingBox"": [1.0, 2.0, 2.0, 2.5],
                                    ""confidence"": 0.98,
                                    ""spans"": [{ ""offset"": 30, ""length"": 4 }]
                                },
                                ""LastName"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""DOE"",
                                    ""content"": ""DOE"",
                                    ""boundingBox"": [2.1, 2.0, 3.0, 2.5],
                                    ""confidence"": 0.97,
                                    ""spans"": [{ ""offset"": 35, ""length"": 3 }]
                                }
                            },
                            ""confidence"": 0.96
                        }
                    ]
                }";

                return JsonSerializer.Deserialize<AnalyzeResult>(json);
            }

            public static AnalyzeResult CreateLowConfidencePassportResult()
            {
                var json = @"{
                    ""apiVersion"": ""2023-07-31"",
                    ""modelId"": ""prebuilt-document"",
                    ""stringIndexType"": ""textElements"",
                    ""content"": ""PASSPORT\nUnited States of America\nJ... D..\nPassport No: 1234...\nDate of Birth: .../01/1990"",
                    ""pages"": [
                        {
                            ""pageNumber"": 1,
                            ""angle"": 0,
                            ""width"": 8.5,
                            ""height"": 11,
                            ""unit"": ""inch"",
                            ""lines"": [
                                {
                                    ""content"": ""PASSPORT"",
                                    ""boundingBox"": [1.0, 1.0, 3.0, 1.0, 3.0, 1.5, 1.0, 1.5],
                                    ""spans"": [{ ""offset"": 0, ""length"": 8 }]
                                },
                                {
                                    ""content"": ""J... D.."",
                                    ""boundingBox"": [1.0, 2.0, 3.0, 2.0, 3.0, 2.5, 1.0, 2.5],
                                    ""spans"": [{ ""offset"": 30, ""length"": 7 }]
                                }
                            ]
                        }
                    ],
                    ""documents"": [
                        {
                            ""docType"": ""document"",
                            ""boundingRegions"": [
                                {
                                    ""pageNumber"": 1,
                                    ""boundingBox"": [0, 0, 8.5, 0, 8.5, 11, 0, 11]
                                }
                            ],
                            ""fields"": {
                                ""DocumentType"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""passport"",
                                    ""content"": ""passport"",
                                    ""boundingBox"": [1.0, 1.0, 3.0, 1.5],
                                    ""confidence"": 0.85,
                                    ""spans"": [{ ""offset"": 0, ""length"": 8 }]
                                },
                                ""FirstName"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""J..."",
                                    ""content"": ""J..."",
                                    ""boundingBox"": [1.0, 2.0, 1.5, 2.5],
                                    ""confidence"": 0.25,
                                    ""spans"": [{ ""offset"": 30, ""length"": 4 }]
                                },
                                ""LastName"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""D.."",
                                    ""content"": ""D.."",
                                    ""boundingBox"": [2.1, 2.0, 2.6, 2.5],
                                    ""confidence"": 0.30,
                                    ""spans"": [{ ""offset"": 35, ""length"": 3 }]
                                }
                            },
                            ""confidence"": 0.35
                        }
                    ]
                }";

                return JsonSerializer.Deserialize<AnalyzeResult>(json);
            }

            public static AnalyzeResult CreateBlurredPassportResult()
            {
                var json = @"{
                    ""apiVersion"": ""2023-07-31"",
                    ""modelId"": ""prebuilt-document"",
                    ""stringIndexType"": ""textElements"",
                    ""content"": ""PASSPORT\n[Blurred text]\n[Unreadable]\nPassport No: [Blurred]\nDate of Birth: [Blurred]"",
                    ""pages"": [
                        {
                            ""pageNumber"": 1,
                            ""angle"": 0,
                            ""width"": 8.5,
                            ""height"": 11,
                            ""unit"": ""inch"",
                            ""lines"": [
                                {
                                    ""content"": ""PASSPORT"",
                                    ""boundingBox"": [1.0, 1.0, 3.0, 1.0, 3.0, 1.5, 1.0, 1.5],
                                    ""spans"": [{ ""offset"": 0, ""length"": 8 }]
                                }
                            ]
                        }
                    ],
                    ""documents"": [
                        {
                            ""docType"": ""document"",
                            ""boundingRegions"": [
                                {
                                    ""pageNumber"": 1,
                                    ""boundingBox"": [0, 0, 8.5, 0, 8.5, 11, 0, 11]
                                }
                            ],
                            ""fields"": {
                                ""DocumentType"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""passport"",
                                    ""content"": ""passport"",
                                    ""boundingBox"": [1.0, 1.0, 3.0, 1.5],
                                    ""confidence"": 0.70,
                                    ""spans"": [{ ""offset"": 0, ""length"": 8 }]
                                }
                            },
                            ""confidence"": 0.15
                        }
                    ]
                }";

                return JsonSerializer.Deserialize<AnalyzeResult>(json);
            }
        }

        public static class DriverLicenseMocks
        {
            public static AnalyzeResult CreateHighConfidenceDriverLicenseResult()
            {
                var json = @"{
                    ""apiVersion"": ""2023-07-31"",
                    ""modelId"": ""prebuilt-document"",
                    ""stringIndexType"": ""textElements"",
                    ""content"": ""DRIVER LICENSE\nState of California\nJANE SMITH\nLicense No: D1234567\nDate of Birth: 05/15/1985"",
                    ""pages"": [
                        {
                            ""pageNumber"": 1,
                            ""angle"": 0,
                            ""width"": 8.5,
                            ""height"": 5.4,
                            ""unit"": ""inch"",
                            ""lines"": [
                                {
                                    ""content"": ""DRIVER LICENSE"",
                                    ""boundingBox"": [1.0, 1.0, 4.0, 1.0, 4.0, 1.5, 1.0, 1.5],
                                    ""spans"": [{ ""offset"": 0, ""length"": 14 }]
                                },
                                {
                                    ""content"": ""JANE SMITH"",
                                    ""boundingBox"": [1.0, 2.0, 3.5, 2.0, 3.5, 2.5, 1.0, 2.5],
                                    ""spans"": [{ ""offset"": 35, ""length"": 10 }]
                                }
                            ]
                        }
                    ],
                    ""documents"": [
                        {
                            ""docType"": ""document"",
                            ""boundingRegions"": [
                                {
                                    ""pageNumber"": 1,
                                    ""boundingBox"": [0, 0, 8.5, 0, 8.5, 5.4, 0, 5.4]
                                }
                            ],
                            ""fields"": {
                                ""DocumentType"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""driver license"",
                                    ""content"": ""driver license"",
                                    ""boundingBox"": [1.0, 1.0, 4.0, 1.5],
                                    ""confidence"": 0.94,
                                    ""spans"": [{ ""offset"": 0, ""length"": 14 }]
                                },
                                ""FirstName"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""JANE"",
                                    ""content"": ""JANE"",
                                    ""boundingBox"": [1.0, 2.0, 2.0, 2.5],
                                    ""confidence"": 0.96,
                                    ""spans"": [{ ""offset"": 35, ""length"": 4 }]
                                },
                                ""LastName"": {
                                    ""type"": ""string"",
                                    ""valueString"": ""SMITH"",
                                    ""content"": ""SMITH"",
                                    ""boundingBox"": [2.1, 2.0, 3.5, 2.5],
                                    ""confidence"": 0.95,
                                    ""spans"": [{ ""offset"": 40, ""length"": 5 }]
                                }
                            },
                            ""confidence"": 0.93
                        }
                    ]
                }";

                return JsonSerializer.Deserialize<AnalyzeResult>(json);
            }
        }

        public static class WrongDocumentTypeMocks
        {
            public static AnalyzeResult CreateDriverLicenseWhenPassportExpected()
            {
                // Returns a driver license result when passport was expected
                return DriverLicenseMocks.CreateHighConfidenceDriverLicenseResult();
            }

            public static AnalyzeResult CreatePassportWhenDriverLicenseExpected()
            {
                // Returns a passport result when driver license was expected
                return PassportMocks.CreateHighConfidencePassportResult();
            }
        }

        public static class ErrorMocks
        {
            public static RequestFailedException CreateServiceUnavailableException()
            {
                return new RequestFailedException(503, "Service temporarily unavailable");
            }

            public static RequestFailedException CreateInvalidApiKeyException()
            {
                return new RequestFailedException(401, "Invalid API key");
            }

            public static RequestFailedException CreateRateLimitException()
            {
                return new RequestFailedException(429, "Rate limit exceeded");
            }
        }

        // Helper methods for creating test scenarios
        public static class TestScenarios
        {
            public static AnalyzeResult CreateScenario(string documentType, float confidence, bool isBlurred = false)
            {
                return documentType.ToLower() switch
                {
                    "passport" when confidence >= 0.85f && !isBlurred => PassportMocks.CreateHighConfidencePassportResult(),
                    "passport" when confidence < 0.50f || isBlurred => PassportMocks.CreateBlurredPassportResult(),
                    "passport" => PassportMocks.CreateLowConfidencePassportResult(),
                    "driverlicense" when confidence >= 0.85f && !isBlurred => DriverLicenseMocks.CreateHighConfidenceDriverLicenseResult(),
                    _ => PassportMocks.CreateLowConfidencePassportResult()
                };
            }

            public static AnalyzeResult CreateMismatchScenario(string expectedType, string actualType)
            {
                return (expectedType.ToLower(), actualType.ToLower()) switch
                {
                    ("passport", "driverlicense") => WrongDocumentTypeMocks.CreateDriverLicenseWhenPassportExpected(),
                    ("driverlicense", "passport") => WrongDocumentTypeMocks.CreatePassportWhenDriverLicenseExpected(),
                    _ => PassportMocks.CreateHighConfidencePassportResult()
                };
            }
        }
    }
}
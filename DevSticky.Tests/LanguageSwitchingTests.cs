using System;
using System.Globalization;
using DevSticky.Services;
using Xunit;

namespace DevSticky.Tests
{
    /// <summary>
    /// Tests to verify language switching functionality.
    /// Validates: Requirements 3.4
    /// </summary>
    public class LanguageSwitchingTests
    {
        [Fact]
        public void LocalizationService_ShouldSwitchToEnglish()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act
            service.SetCulture("en");
            var result = service.GetString("AppName");
            
            // Assert
            Assert.Equal("DevSticky", result);
            Assert.Equal("en", service.CurrentCulture.TwoLetterISOLanguageName);
        }

        [Fact]
        public void LocalizationService_ShouldSwitchToVietnamese()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act
            service.SetCulture("vi");
            var okText = service.GetString("OK");
            var cancelText = service.GetString("Cancel");
            
            // Assert
            Assert.Equal("Đồng ý", okText);
            Assert.Equal("Hủy", cancelText);
            Assert.Equal("vi", service.CurrentCulture.TwoLetterISOLanguageName);
        }

        [Fact]
        public void LocalizationService_ShouldSwitchBetweenLanguages()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act & Assert - Switch to English
            service.SetCulture("en");
            Assert.Equal("Save", service.GetString("Save"));
            Assert.Equal("Delete", service.GetString("Delete"));
            
            // Act & Assert - Switch to Vietnamese
            service.SetCulture("vi");
            Assert.Equal("Lưu", service.GetString("Save"));
            Assert.Equal("Xóa", service.GetString("Delete"));
            
            // Act & Assert - Switch back to English
            service.SetCulture("en");
            Assert.Equal("Save", service.GetString("Save"));
            Assert.Equal("Delete", service.GetString("Delete"));
        }

        [Fact]
        public void LocalizationService_ShouldHandleFormattedStrings()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act - English
            service.SetCulture("en");
            var englishFormatted = service.GetString("NoteCount", 5);
            
            // Act - Vietnamese
            service.SetCulture("vi");
            var vietnameseFormatted = service.GetString("NoteCount", 5);
            
            // Assert
            Assert.Equal("5 note(s)", englishFormatted);
            Assert.Equal("5 ghi chú", vietnameseFormatted);
        }

        [Fact]
        public void LocalizationService_ShouldReturnKeyForMissingTranslation()
        {
            // Arrange
            var service = LocalizationService.Instance;
            var nonExistentKey = "NonExistentKey_12345";
            
            // Act
            service.SetCulture("en");
            var result = service.GetString(nonExistentKey);
            
            // Assert
            Assert.Equal(nonExistentKey, result);
        }

        [Fact]
        public void LocalizationService_ShouldFireLanguageChangedEvent()
        {
            // Arrange
            var service = LocalizationService.Instance;
            var eventFired = false;
            service.LanguageChanged += (sender, args) => eventFired = true;
            
            // Act
            service.SetCulture("en");
            
            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public void LocalizationService_ShouldSupportAllDefinedCultures()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act & Assert
            foreach (var culture in LocalizationService.SupportedCultures)
            {
                service.SetCulture(culture.TwoLetterISOLanguageName);
                var appName = service.GetString("AppName");
                
                // AppName should always be "DevSticky" regardless of language
                Assert.Equal("DevSticky", appName);
            }
        }

        [Fact]
        public void StaticHelper_ShouldAccessLocalization()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act - English
            service.SetCulture("en");
            var englishText = L.Get("Dashboard");
            
            // Act - Vietnamese
            service.SetCulture("vi");
            var vietnameseText = L.Get("Dashboard");
            
            // Assert
            Assert.Equal("Dashboard", englishText);
            Assert.Equal("Bảng điều khiển", vietnameseText);
        }

        [Fact]
        public void LocalizationService_ShouldHandleInvalidCultureGracefully()
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act
            service.SetCulture("invalid-culture-code");
            
            // Assert - Should fallback to English
            Assert.Equal("en", service.CurrentCulture.TwoLetterISOLanguageName);
        }

        [Theory]
        [InlineData("NewNote", "en", "+ New Note")]
        [InlineData("NewNote", "vi", "+ Ghi chú mới")]
        [InlineData("Settings", "en", "Settings")]
        [InlineData("Settings", "vi", "Cài đặt")]
        [InlineData("Dashboard", "en", "Dashboard")]
        [InlineData("Dashboard", "vi", "Bảng điều khiển")]
        public void LocalizationService_ShouldReturnCorrectTranslation(string key, string culture, string expected)
        {
            // Arrange
            var service = LocalizationService.Instance;
            
            // Act
            service.SetCulture(culture);
            var result = service.GetString(key);
            
            // Assert
            Assert.Equal(expected, result);
        }
    }
}

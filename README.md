# 🚀 Price Tracker Script

> **Web scraping ve veri toplama scripti** - .NET 8 ile geliştirilmiş, retry politikası ve logging ile güçlendirilmiş profesyonel veri çekme aracı.

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/alknbugra/price-tracker-script.svg)](https://github.com/alknbugra/price-tracker-script/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/alknbugra/price-tracker-script.svg)](https://github.com/alknbugra/price-tracker-script/network)

## 📋 İçindekiler

- [Özellikler](#-özellikler)
- [Gereksinimler](#-gereksinimler)
- [Kurulum](#-kurulum)
- [Kullanım](#-kullanım)
- [Konfigürasyon](#-konfigürasyon)
- [Özelleştirme](#-özelleştirme)
- [Performans](#-performans)
- [Hata Yönetimi](#-hata-yönetimi)
- [Proje Yapısı](#-proje-yapısı)
- [API Referansı](#-api-referansı)
- [Troubleshooting](#-troubleshooting)
- [Katkıda Bulunma](#-katkıda-bulunma)
- [Lisans](#-lisans)

## ✨ Özellikler

### 🎯 Temel Özellikler

- **🌐 HTTP İstekleri** - HttpClient ile güvenli web veri çekme
- **🔄 Retry Politikası** - Polly ile otomatik hata yönetimi (3 deneme)
- **📊 HTML Parse** - HtmlAgilityPack ile XPath seçiciler
- **📝 CSV Çıktı** - CsvHelper ile düzenli veri export
- **📋 Logging** - Serilog ile konsol + dosya loglama
- **⚙️ Konfigürasyon** - JSON dosyasından esnek ayar yönetimi

### 🛠️ Teknik Özellikler

- **.NET 8** modern framework
- **HttpClientFactory** ile socket yönetimi
- **Polly** retry pattern (exponential backoff)
- **HtmlAgilityPack** HTML parsing
- **Serilog** structured logging
- **CsvHelper** CSV export
- **Configuration** JSON settings

## 🔧 Gereksinimler

### Donanım Gereksinimleri

- **Windows 10/11** (64-bit önerilen)
- **Minimum 2GB RAM**
- **100MB boş disk alanı**
- **İnternet bağlantısı**

### Yazılım Gereksinimleri

- **.NET 8 SDK** veya üzeri
- **Visual Studio 2022** (geliştirme için)
- **PowerShell 7** (opsiyonel)

## 🚀 Kurulum

### 1. Repository'yi Klonlayın

\\\ash
git clone https://github.com/alknbugra/price-tracker-script.git
cd price-tracker-script

## 📖 Kullanım

### Temel Kullanım

1. **\ppsettings.json\** dosyasını düzenleyin
2. **Hedef URL'leri** ekleyin
3. **Scripti çalıştırın** - \dotnet run\" >> README.md && echo 
4.
**\output.csv\**
dosyasını
kontrol
edin >> README.md && echo " >> README.md && echo ###
Hızlı
Başlangıç >> README.md && echo " >> README.md && echo \\\ash >> README.md && echo #
Proje
klasörüne
git >> README.md && echo cd
src/PriceTracker.Script >> README.md && echo " >> README.md && echo #
Varsayılan
ayarlarla
çalıştır >> README.md && echo dotnet
run >> README.md && echo " >> README.md && echo #
Çıktıyı
kontrol
et >> README.md && echo type
output.csv >> README.md && echo \\\"

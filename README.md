# MonsterWebHost

### Self-Hosted Website Hosting Platform for Windows

**MonsterWebHost** is a modern Windows desktop application built with **C#**, **.NET 8**, **WPF**, and **ASP.NET Core Kestrel** that allows users to host websites directly from a local folder while providing real-time monitoring, Cloudflare Tunnel integration, analytics, logging, and management tools.

MonsterWebHost aims to provide a lightweight alternative to traditional web servers such as IIS for developers, small businesses, homelabs, and anyone who wants to publish websites quickly without requiring complex server configuration.

---

# Overview

MonsterWebHost transforms any folder containing a website into a locally hosted web server powered by ASP.NET Core Kestrel.

Instead of configuring IIS, Apache or Nginx, MonsterWebHost allows users to:

* Select a website folder
* Start a local web server
* Automatically monitor folder changes
* Publish the site through Cloudflare Tunnel
* Monitor visitors in real time
* Track downloads
* View analytics
* Export logs
* Manage multiple websites

The long-term vision is to become a complete Windows website hosting platform that combines the simplicity of a desktop application with the capabilities of a lightweight web server management console.

---

# Description

MonsterWebHost provides a graphical interface for hosting static websites and, in future releases, ASP.NET Core applications directly from Windows.

The application is designed around ASP.NET Core's Kestrel server and integrates monitoring, analytics, Cloudflare connectivity, logging, security, and telemetry into a single desktop application.

Unlike traditional hosting software, MonsterWebHost focuses on simplicity while remaining production-oriented.

---

# Features

## Website Hosting

* Host websites from any folder
* Static HTML hosting
* CSS
* JavaScript
* Images
* Fonts
* Media
* Asset serving
* MIME detection
* Content compression
* HTTP/1.1
* HTTP/2
* HTTPS support
* Automatic folder reload

---

## Website Management

Manage one or more websites from a single application.

Each hosted website stores:

* Name
* Local folder
* Local URL
* Port
* Status
* Domain
* SSL status
* Cloudflare status
* Analytics
* Logging settings

---

## Folder Browser

Browse hosted website folders directly inside the application.

Displays

* Files
* Folders
* Images
* HTML
* CSS
* JavaScript
* Assets
* Icons

Supports automatic refreshing when files change.

---

## Folder Monitoring

Uses FileSystemWatcher to monitor websites in real time.

Automatically detects

* File creation
* File deletion
* File updates
* File renames
* Directory changes

Changes become available without restarting the application.

---

# ASP.NET Core Kestrel

MonsterWebHost uses ASP.NET Core Kestrel as its web server.

Benefits include:

* High performance
* Modern HTTP pipeline
* Cross-platform hosting engine
* Secure request processing
* Minimal memory footprint
* Excellent scalability

---

# Cloudflare Tunnel Integration

MonsterWebHost is designed to integrate with Cloudflared.

Capabilities include

* Start tunnel
* Stop tunnel
* Restart tunnel
* Monitor tunnel status
* View tunnel logs
* Display public URL
* Domain binding
* DNS validation

The application can launch Cloudflared as a managed background process while monitoring its health.

---

# Dashboard

The dashboard provides real-time information about hosted websites.

Displays

* Server status
* Online/Offline indicator
* Uptime
* Requests per second
* Total requests
* Active users
* Connected clients
* CPU usage
* Memory usage
* Network throughput
* Disk activity
* Current bandwidth

---

# Analytics

MonsterWebHost stores analytics locally using SQLite.

Collected information may include:

* Request history
* Response times
* Visitor count
* Bandwidth
* Downloads
* Page views
* Browser statistics
* Operating systems
* Countries
* Cities (where available)
* Referrers

---

# Download Tracking

For downloadable files the application records:

* Filename
* Download time
* Client IP
* User Agent
* Bytes transferred
* Referrer
* HTTP status
* Duration

Optional geolocation can be performed using configurable IP geolocation providers. IP-based locations are approximate and should be used in compliance with applicable privacy laws.

---

# Logging

MonsterWebHost includes detailed logging.

Log categories

* Hosting
* Requests
* Downloads
* Security
* Cloudflare
* Errors
* Exceptions
* Folder Monitoring

Supports

* Daily rolling logs
* JSON logs
* CSV export
* Plain text logs

---

# Security

MonsterWebHost is designed with production deployment in mind.

Features include

* HTTPS redirection
* Secure headers
* CORS configuration
* Request validation
* Rate limiting
* IP allow list
* IP block list
* Error handling
* Exception logging

---

# Multi-Site Hosting

Host multiple websites simultaneously.

Each website may have

* Separate folder
* Separate port
* Separate domain
* Separate analytics
* Separate logs
* Separate Cloudflare tunnel

---

# Performance Monitoring

The application monitors itself while hosting.

Metrics include

* CPU
* RAM
* Requests
* Response time
* Active connections
* Network usage
* Hosting uptime

---

# Project Architecture

```text
MonsterWebHost
│
├── UI
│   ├── Dashboard
│   ├── Website Manager
│   ├── Folder Explorer
│   ├── Analytics
│   ├── Downloads
│   ├── Logs
│   ├── Security
│   └── Settings
│
├── Hosting
│   ├── Kestrel
│   ├── Static Files
│   ├── MIME Provider
│   ├── Compression
│   └── HTTPS
│
├── Cloudflare
│   ├── Tunnel Manager
│   ├── DNS
│   ├── Status
│   └── Logs
│
├── Monitoring
│   ├── Dashboard
│   ├── Performance
│   ├── Requests
│   └── Downloads
│
├── Analytics
│   ├── SQLite
│   ├── Visitors
│   ├── Countries
│   ├── Browsers
│   └── Reports
│
├── Logging
│
├── Security
│
└── Services
```

---

# Technology Stack

* C#
* .NET 8
* WPF
* ASP.NET Core
* Kestrel
* SQLite
* Cloudflared
* FileSystemWatcher
* System.Diagnostics
* System.Net.Http
* JSON
* MVVM Architecture

---

# Future Roadmap

The following capabilities are planned for future releases:

* Reverse proxy management
* Automatic Let's Encrypt certificates
* Docker deployment support
* ASP.NET Core application deployment
* Website deployment wizard
* Backup and restore
* Configuration import/export
* Live traffic graphs
* Historical analytics dashboards
* Real-time visitor map
* Plugin architecture
* REST API
* PowerShell automation
* Scheduled website publishing
* Website health monitoring
* Automatic update system
* Built-in file editor
* SSL certificate manager
* Compression optimization
* CDN integration
* HTTP/3 support
* WebSocket monitoring

---

# Requirements

* Windows 11
* .NET 8 Runtime
* Cloudflared (optional, for public access)
* SQLite (embedded)
* Visual Studio 2022 (for development)

---

# Building

```bash
dotnet restore

dotnet build

dotnet run
```

---

# Vision

MonsterWebHost is designed to simplify self-hosting by combining a desktop management experience with the power of ASP.NET Core. The goal is to provide developers, hobbyists, and small organizations with an approachable yet capable hosting platform that can grow from serving a single static website to managing multiple hosted applications with integrated monitoring, analytics, and Cloudflare connectivity.

# SwitchManager

A professional WPF application for managing and monitoring network switch ports via Serial/Console connection.

## Overview
This tool automates the process of auditing physical switch ports and synchronizing them with a logical configuration defined in JSON. It uses asynchronous communication and Regex parsing to interact with Cisco-like CLI environments.

##  Key Features
* **Hardware Audit**: Real-time status retrieval (`connected`, `notconnect`, `disabled`).
* **VLAN Auto-Fix**: Automatically aligns physical port VLANs with the desired configuration.
* **MVVM Pattern**: Clean separation of concerns using `RelayCommand` and `IsBusy` state management.
* **Resilient Communication**: Handles session timeouts and COM port exceptions gracefully.

## How It Works
The application executes the `show interface status` command, parses the output using Regex, and updates the UI models.

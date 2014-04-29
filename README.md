# HashPeak

## Introduction

### What's this?

Determining the most effecient hashrate for GPU-based cryptocurrency mining is a tedious process. It usually involves manually trying a large number of GPU engine core frequencies and keeping track of the resulting hashrate to detect any peaks in hashrate. HashPeak automates this process.

HashPeak is a .NET 2.0 console application that connects to a running instance of sgminer or cgminer, sets a GPU engine clock, waits for the hashrate to stabilize and then measures the hashrate. The application is provided with a range of GPU clocks to test and then runs through them in succession. Upon completion, the application presents the user with the lowest GPU enging clock frequency that resulted in the highest possible hashrate.

In addition, HashPeak generates a CSV (Comma-Separated Values) file including all measured data as well as a plotted graph in the form of a PNG file.

### License

HashPeak is open source software released under the MIT License. See `LICENSE.md` for details.

### Donations

If you like HashPeak and have some cryptocurrency burning a hole in your pocket, by all means.

```
BTC:  1L6zn1zPVxFzbucip7AE7LH7aU2s3haHTL
LTC:  LS1c68c6DgvMMkV4mqgwXJboo2HCws9VKc
BC:   BGfHDAkiNA8uMXLgHdaW2oXqeJv11cCjXZ
DOGE: DTwrM6GQNXvrtVcGW9smpEC19mDaWzXcRE
```

## Usage

### API access

In order for HashPeak to be able to connect to and communicate with sgminer/cgminer, *the miner needs to have its API enabled and needs to be configured to allow connections from the IP address that HashPeak is running on*. In addition, HashPeak needs privileged access to the API in order to set GPU engine clocks. If the miner is running locally, this is typically achived by running the miner with the command line arguments `--api-listen --api-allow W:127.0.0.1`. If the miner is running on a separate machine, `127.0.0.1` needs to be replaced with the IP from which you are connecting. More information about the API command line arguments can be found in the documentation for your miner.

### Miners

The recommended miner to use with HashPeak is sgminer. Although HashPeak works with cgminer, and most likely bfgminer as well, the precision of hashrates reported through the API is very limited in cgminer. In sgminer, the precision is down to a tenth of a kilohash/s, but in cgminer, the precision is 10 kilohashes/s.

### Command line arguments

    Options:
        --host=VALUE           IP or hostname for miner API. Default: 127.0.0.1.
        --port=VALUE           Port number for miner API. Default: 4028.
        --gpu-id=VALUE         GPU ID to work on. [required]
        --min-gpu-clock=VALUE  Lower limit of GPU engine clock frequency range to
                                 test. [required]
        --max-gpu-clock=VALUE  Upper limit of GPU engine clock frequency range to
                                 test. [required]
        --step=VALUE           Number of MHz to increase GPU engine clock per
                                 iteration. Default 1.
        --delay=VALUE          Seconds to wait between setting new clock and
                                 testing the hashrate. Default: auto.
        --help                 Show this message and exit.

Please note that setting `step` > 1 will result in faster completion of the measurements, but will lower the resolution of the data. The most effecient way to detect a peak is to initially run with a step value of e.g. 5 and let the tests complete. Then review the data, find the sweetspot you're interested in and run a second time with step set to 1, but with a much more limited range (e.g. +-5 from the peak).

The default `delay` value is `auto`. In this mode, HashPeak will try to determine when the hashrate has stabilized after setting a new GPU clock. If no stable hashrate has been achived after 30 seconds, the hashrate at that moment will be registered. A manual timeout in seconds can be specified if the autodelay feature is causing problems. The autodelay feature is disabled when running against cgminer.

In order to maximize the accuracy of HashPeak, the miner instance should ideally be mining a stable scrypt coin, either solo or on a non coin-switching pool. If the pool switches coins frequently or has problems providing the miner with work, the recorded hashrates can fluctuate resulting in incorrect or misleading data.

### Samples

Run HashPeak on GPU with ID 0 on the local miner instance, measuring the hashrate for GPU clocks 950-1025 MHz:

```
HashPeak.exe --gpu-id 0 --min-gpu-clock 950 --max-gpu-clock 1025
```

Run HashPeak on GPU with ID 3 on the local miner instance, measuring the hashrate for GPU clocks 900-1100 MHz with a step of 5 and a delay of 20 seconds:

```
HashPeak.exe --gpu-id 3 --min-gpu-clock 900 --max-gpu-clock 1100 --step 5 --delay 20
```

Run HashPeak on GPU with ID 0 on a remote miner instance with custom port:

```
HashPeak.exe --host 192.168.0.47 --port 1337 --gpu-id 0 --min-gpu-clock 950 --max-gpu-clock 1025
```

## Building

Coming soon.

### Dependencies

- Json.NET
- ZedGraph

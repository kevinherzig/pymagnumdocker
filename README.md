**Pymagnum Hardware & Docker Image**

I liked the work that Charles Godwin did in creating a Python based platform for reading data from the Magnum Energy series of inverters.  After spending a good amount of time setting it up and using it, I wanted to contribute back to the community.

I created a docker image tested on simple, cheap hardware.  I'll highlight the hardware and provide set up instructions.

This is an advanced undertaking.  I'm not going to get into details about how to install the OS, set it all up, use SSH to install things, and configure your router for access. 

**HARDWARE**

This image runs great on a Raspberry PI 2W.  
[link](https://www.amazon.com/dp/B09LH5SBPS?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_2)
![enter image description here](https://m.media-amazon.com/images/I/61rVLsPzmeL._AC_SX679_.jpg)

An SD Card to boot the system
[SD Card](https://www.amazon.com/Amazon-Basics-Reader-Adapter-1-Pack/dp/B0DB5BW783/ref=sr_1_14?dib=eyJ2IjoiMSJ9.WYqHlY2vJkPfbe6PrvA33GYloKYpVJQl7fYgHWrk3d03rjBdDwnitNFlFOZ-H3QLR4IuWv3Gn6I-JEmdy4fZjaS1vH5d5oQyFgy4lk3cPKr75N6eUPU76i6jTLBDbJuP_CJV9zVZgeaKl4JxqTk2R5PhB_JjTIxAEMjovVcaAlfSd3TesYV1-YGn5phF_nBv0uEtl635Y0owernnpheLTCO_rx6k4mzm0tlX49JHt74.fzw9mNhci5qddmFUd51ABp-8QBsfI835uV5ac3Rk3HY&dib_tag=se&keywords=sd%2bcard&qid=1767103560&sr=8-14&th=1)

Next you'll need a RS485 Interface.
https://www.amazon.com/dp/B0DR218X7P?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_2

![enter image description here](https://m.media-amazon.com/images/I/610CxeyhJdL._AC_SY300_SX300_QL70_FMwebp_.jpg)


Finally you'll need a way to plug in regular USB-A devices (the above RS485 interface) into the Raspberry PI 2W.
https://www.amazon.com/dp/B083WML1XB?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_1&th=1
![enter image description here](https://m.media-amazon.com/images/I/61maT4U8lSL._AC_SX679_.jpg)

Optinally a HDMI cable in case you need to debug what's going on with the OS
[link](https://www.amazon.com/UGREEN-Adapter-Ethernet-Compatible-Raspberry/dp/B06WW3LPWL/ref=sr_1_5?crid=3L490GV5BXJS9&dib=eyJ2IjoiMSJ9.iSNSg-dq1X6kRDNJ3GAACg9hweY8PCjgVpCmNt7CmfRtDTPIvne2cyjg96vRFJL16pPnq8Qnq-a6m9KcbW3wyd9r-0gg2_VByr37iSnRVGcMbw-SaGPdzB4HNhVLKleQfnSHyE_y_9Fqks_iMZwbqsSBSb0XGTE_2SGJCw19IRDJV0tOy_sqPh7MWaHAXqOBuRSdP0FSNPrgsjc-qxoXLnOCCDeBZPwQmC5Hygy0tkw.7xf68af2SgTGhVT9Vg2DPR0hiAciYP5sRaoLKaLDGn0&dib_tag=se&keywords=raspberry%2bpi%2b2w%2bhdmi%2bcable&qid=1767105269&sprefix=raspberry%2bpi%2b2w%2bhdmi%2bcabl,aps,151&sr=8-5&th=1)

A RJ-11 Splitter
https://www.amazon.com/dp/B0CG97KWV6?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_7&th=1
![enter image description here](https://m.media-amazon.com/images/I/51tR7kVIW+L._SX522_.jpg)

And some RJ11 telephone cable
https://www.amazon.com/dp/B08L8MQWFL?ref_=ppx_hzsearch_conn_dt_b_fed_asin_title_8&th=1

**Raspberry PI Setup**

Follow the instructions from the Raspberry PI web page to flash the SD card.  I recommend installing the Raspberry PI Lite version of the O/S since the 2W is very memory limited an and a desktop will add a lot of memory and CPU overhead.

I set up the Wifi access point and Raspberry PI password through the installer.  That way I can boot it, look at my router (or the boot up screen of the PI) to find the IP address, and SSH into it to install the rest.

[link](https://www.raspberrypi.com/documentation/computers/getting-started.html)

**Magnum  Docker Setup**
Once you are logged into the PI, you can install pymagnumdocker.

First we need to install docker
`sudo apt install docker.io docker-compose`

Next pull the git repo:
`git clone https://github.com/kevinherzig/pymagnumdocker`
`cd pymagnumdocker`

Now build the image.  On the 2W this will take a few minutes.
`sh build.sh`

Once built you can run the image:
`docker compose up`


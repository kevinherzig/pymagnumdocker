First run build.sh to build the docker image.

then run 

docker compose up

to start the server

**Pymagnum Hardware & Docker Image**

I liked the work that Charles Godwin did in creating a Python based platform for reading data from the Magnum Energy series of inverters.  After spending a good amount of time setting it up and using it, I wanted to contribute back to the community.

I created a docker image tested on simple, cheap hardware.  I'll highlight the hardware and provide set up instructions.

Since this will run at the location the inverter is installed at, you'll need to have enough of an understanding of how to proxy http connections to be able to use this from the outside world.  If there is interest I might try to automate this portion, but for now...

At the moment the service is not completely stable.   
> Written with [StackEdit](https://stackedit.io/).

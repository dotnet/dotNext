Multiplexing
====

.NEXT exposes a simple multiplexing protocol on top of TCP, so the single TCP connection can be shared between the independent components of the application. Its design is very similar to the existing implementations:

* [yamux](https://github.com/hashicorp/yamux)
* [smux](https://github.com/xtaci/smux)
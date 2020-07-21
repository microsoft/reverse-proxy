---
name: Usage Questionaire
about: Help us understand your scenarios for using a reverse-proxy
labels: "Type: Survey-Response"
---

# Intro
We have a pretty good idea of he core feature set for 1.0, see the board for more details. What we'd like to really understand is how you are currently or envision using a reverse proxy in your deployments. This set of questions is designed to solicit as much information as possible, and is kind of long. Answer as much or little as you feel comfortable with. If you want to respond privately, please send a message via github or to yarp-feedback@microsoft.com
 
# Terminology (for this questionnaire)
 
- EndPoint - an IP+Port+Scheme combo representing a socket that can be connected to
- Backend - a collection of server EndPoints to which a requests can be routed, assuming the server functionality is stateless
- Route - a mapping from a front endpoint on the proxy to the backends dependent on specific data as part of the request, including
  - Path
  - Host headers
  - Custom headers
  - Load balancing - choosing which endpoint from the list of a back end to route an individual request to.
 
 
# Questions
 
## Front Endpoint

### How many individual EndPoints does each proxy server listen to (count)?
- [ ] Port 80
- [ ] Port 443
- [ ] Other ports - how many (not the port numbers)
  - [ ] 1 to 10
  - [ ] 11 to 100
  - [ ] 101 to 1000
  - [ ] More than 1000

### How often does the list of endpoints change?
_Do you dynamically add/remove IP:Port:scheme listening to the front end of the proxy?_ 
- [ ] Never
- [ ] Weekly or less frequently
- [ ] Daily to Weekly
- [ ] Hourly to daily
- [ ] Every few minutes or more often

<!-- Why? -->

### For HTTPS EndPoints, how many name/cert combinations do you support per server?
- [ ] 1
- [ ] 2 to 10
- [ ] 11 to 100
- [ ] 101 to 1000
- [ ] More than 1000

### How are the certs managed/stored?

<!-- Do you store them in the OS cert store, if not where do they get fetched from? -->

### How often do certs get added/removed?
- [ ] Never
- [ ] Weekly or less frequently
- [ ] Daily to Weekly
- [ ] Hourly to daily
- [ ] Every few minutes or more often

----
## Routes
### What criteria are used to compose a route?

<!--
    Do you look at headers (other than host)?
    Do you look at Verbs, Paths, Query String ?
    What is the typical fanout of that criteria - eg do you look at multiple paths under each hostname?
-->
### How many routes does each proxy server need to support?
- [ ] 1
- [ ] 2 to 10
- [ ] 11 to 100
- [ ] 101 to 1000
- [ ] More than 1000

### Typically, how frequently do the route definitions change?
_But not specific Endpoints for the route, that's asked further below_
- [ ] Never
- [ ] Weekly or less frequently
- [ ] Daily to weekly
- [ ] Hourly to daily
- [ ] Every few minutes
- [ ] Less than a minute

### Where is the information about routes coming from?
- [ ] Configuration file(s)
- [ ] Kubernetes ingress
- [ ] Other (more details please)

----
## BackEnds & connections to EndPoints
### Typically how many backend endpoints are there for each individual route?
- [ ] 1
- [ ] 2 to 3
- [ ] 4 to 10
- [ ] 11 to 1000
- [ ] More than 1000

### Are the same backend endpoints used for multiple routes?
- [ ] Yes - but the same combination of endpoints is used together
- [ ] Yes - but the endpoints are multiplexed, with different combinations for each route
- [ ] No
- [ ] Other - Please explain?

### How are the endpoints discovered for a route?
<!-- 
    The IP/Port/Scheme are directly specified
    Is DNS involved?
    Does DNS resolve to multiple IP addresses
	Do you need to recycle EndPoints based on expiring DNS TTL's?
-->

### How often do the BackEnd EndPoints change for a route
_eg servers get added/removed_
- [ ] Never
- [ ] Weekly or less frequently
- [ ] Daily to weekly
- [ ] Hourly to daily
- [ ] Every few minutes
- [ ] Less than a minute


### How many HTTP connections do you typically have concurrently to each BackEnd EndPoint?
  - [ ] 1 to 10
  - [ ] 11 to 100
  - [ ] 101 to 1000
  - [ ] More than 1000

### What is the duration of an HTTP connection?
_How many requests will be sent over each connection?_
- [ ] 1
- [ ] 2 to 10
- [ ] 11 to 100
- [ ] 101 to 1000
- [ ] More than 1000

### What criteria are used to determine when to close a connection? 
<!--
    Do you use a Timeout?
    Do you tear down a connection after a certain number of requests?
-->

### Do you need to manage local ephemeral port or choose network interface/local ip address on outbound connections?
<!-- Details -->

----

## Configuration Changes

### We see a model of the YARP proxy having the notion of a live config, being able to create/change the config, and then apply the new config to new requests as an atomic switch. Does this model work for you?
- [ ] Yes
- [ ] No - Why?

<!-- details if applicable -->

### What SLA would you expect from having data for the new config to it being applied to new requests? 
_Aka how fast should config changes be applied?_

<!-- details -->

## Load Balancing
### What load balancing algorithms do you currently use?

### What algorithms do you want to use if not currently available?

### How are you accounting for back end server health as part of current load balancing?
<!-- 
    Are you using metrics for back end performance to aid load balancing?
	- If so what metrics do you look at and where?
	- How often are those metrics updated?
-->

### Where load balancing isn't symmetric - eg for A/B testing - how is that accounted for?

### How often are the parameters for load balancing changed for a specific route?
- [ ] Never
- [ ] Weekly or less frequently
- [ ] Daily to weekly
- [ ] Hourly to daily
- [ ] Every few minutes
- [ ] Less than a minute

### Do all routes use the same balancing algorithm?
- [ ] Yes
- [ ] No

## A/B testing
### Do you use A/B, Red/Green or other mechanism to divide load for rollouts and experiments?
<!--
	- How do you define the back-end groups?
	- How do you pick which requests go to which group?
-->

----

## Affinities
### How do you check if a request is affinitized to a specific back-end endpoint?
- [ ] HTTP Connection
- [ ] Session Cookie
- [ ] URL modification
- [ ] Authenticated User
- [ ] Tenant (User affiliation)

----

## Request Transformation

### Beyond adjusting headers for new host name, x-forwarded, what other header manipulation do you do?

### Do you normalize headers - clean up spacing, change capitalization etc?

### What headers do you add/change for requests going to back end?

### What headers do you add/change for responses going to client?

### Do you do any transformation of the body of the request / response?

----

## Forwarders

### How much connection information do you preserve and forward to the backend?
<!-- 
    - Remote IP
    - Remote port
    - Local IP
    - Local port
    - Scheme
    - client certificate

	- How do you flow that information?
		○ X-Forwarded-* headers?
		○ PPv2 (https://www.haproxy.org/download/1.8/doc/proxy-protocol.txt)?
		○ Related to request transformation
	- Do you receive such information from upstream? In what format?
-->

----

## Logging and metrics
### What properties and when do you log for your proxy today?
<!--
    What do you log on a per request basis?
    What log levels do they apply to ?
-->

### What do you want to be able to log that you don't/can't?

### Is there different logging on a per-request basis?
<!-- 
    Eg based on some data of the request, or meta concept (do a deeper profile of yyy% of all requests)
-->

### What metrics do you currently collect?
<!--
    Can we get a complete list - pointing to code, a log, screenshot etc
-->

### What metrics do you want to collect that you can't currently?

### How often do you aggregate/calculate data based on the metrics?

### In multi-tenant scenarios, what extra data needs to be attached to logs to identify the customer etc?
<!-- 
    Also Where does that data come from?
-->

----

## Traffic Profile
### How many requests / sec are processed by each proxy server / process?
<!--
    On what OS / Hardware combo?
-->

### What latency requirements are there?
<!--
    eg P95 of 200ms for connecting to BackEnd
-->

### What % of incoming requests are over
<!--
    HTTPS vs HTTP
	HTTP 1.0/1.1/2/3
-->

### If you were to plot graphs for metrics for your proxy, What are the graphs for frequency of:

- How many requests / connection

- Percentage of GET vs POST style requests with a request body
  - Size of request payload for POST/PUT etc

- Front end
  - Bytes / Sec of incoming data to front end
  - Bytes/ Sec of response data to end users
  - Duration of front end connections, in KB & Sec

- Back End
  - Bytes / Sec of outgoing data to back end
  - Bytes/ Sec of incoming data from back end
  - Duration of front end connections, in KB & Sec
  - (Assumption is that these are not the same, and that some buffering will need to happen)

## Feature(s)

### Do you do/use distributed tracing?

### What do you do when an individual request fails against the back end?
<!--
    - Do you have any form of redo/retry?
	- Is request based errors used to determine backend server health?
-->

### Do you do inbound request limiting on the Front EndPoints? 
<!-- 
    If so what criteria are used?
-->

### Do you have any form of request prioritization or are all requests treated the same?

### Do you use shadowing in any form? 
<!--
    - By which we mean sending the same request to more than one back end at once
	- What processing are you doing on the results?
-->

### Health Checks
<!--
	Do you have a mechanism verifying back end server health, and adjusting load balancing based on that?
-->

### Do you do any authentication at the proxy ?
<!--
    where the proxy makes decisions based on the auth (passing through headers doesn't count)?
-->

### Do you do any response caching at the proxy?

### Do you do any body compression at the proxy layer?
	 
### What sort of request clean-up is done before forwarding?
<!--
    Conversely what scenarios exist where clean-up is not desired?
-->

### What else have we missed that is important to you?

Thank you for getting this far. Any answers you can provide will help us scope and prioritize features in the proxy.
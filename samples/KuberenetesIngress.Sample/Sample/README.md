# Sample backend

This is a sample backend that can be built into a docker image.

## Building

To build a docker image from this container, you must have docker installed first. See https://docs.docker.com/get-started/#download-and-install-docker.

Run the following, inserting the desired registry name:

```
cd backend
docker build . -t <REGISTRY_NAME>/backend:1.0.0
docker push <REGISTRY_NAME>/backend:1.0.0
```

## Deploying a Kubernetes Ingress

Note: before deploying the ingress, both the YARP Ingress Controller and Ingress must be deployed to the kuberentes cluster.

To deploy this sample, you can simply run from this directory:

```
kubectl apply -f ingress-sample.yaml
```

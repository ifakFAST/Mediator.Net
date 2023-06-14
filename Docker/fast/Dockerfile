FROM ubuntu:jammy

WORKDIR /app

# Assumes that a file "Mediator_Linux64.tar.gz" exists in the same directory as this Dockerfile
COPY ./Mediator_Linux64.tar.gz /app

RUN apt-get update && \
    apt-get install -y tar libicu-dev && \
    tar -xzf Mediator_Linux64.tar.gz && \
    rm Mediator_Linux64.tar.gz && \
    rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["./Bin/Mediator/MediatorCore", "--config=./Config/AppConfig.xml", "--title=ifakFAST", "--logdir=./Data", "--logname=LogFile"]

STOPSIGNAL SIGINT

# Expose default Dashboard port
# Note that the "listen-host" setting in AppConfig.xml must be set to "0.0.0.0" for this to work
EXPOSE 8082

# Use the official Python 3.10 base image
FROM python:3.10-slim

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

# Install system dependencies (optional: for building some Python packages)
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

RUN apt-get install bash -y


# Install pymagnum
RUN pip install --no-cache-dir pymagnum tzlocal

# Set working directory
WORKDIR /app

# Copy your code into the container (optional if you have a project)
COPY . /app

# Default command (can be overridden)
#CMD ["/bin/bash"]

CMD python3 magserver.py

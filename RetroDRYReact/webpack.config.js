const path = require('path');
const pkg = require('./package.json');
const nodeExternals = require('webpack-node-externals');
module.exports = {
    entry: "./index.ts",
    resolve: {
        extensions: [ '.tsx', '.ts', '.js']
    },
    output: {
      path: path.resolve(__dirname, 'dist'),
      filename: "index.js",
      library: pkg.name,
      libraryTarget: "umd",
    },
    target: 'node',
    externals: [nodeExternals()],
    module: {
      rules: [
        {
          test: /\.tsx?$/,
          exclude: /node_modules/,
          use: {
            loader: "ts-loader"
          }
        }
      ]
    }
};